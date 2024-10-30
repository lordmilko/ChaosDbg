using System;
using System.Runtime.InteropServices;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug;
using ClrDebug.DIA;

namespace ChaosDbg
{
    /* As much as I would love to not have to use DbgHelp at all, sadly, after much investigation, it seems we are still dependent on it.
     * Never the less, I spent quite a large amount of time trying to figure out how to get this working, so I will at least document
     * my findings here.
     *
     * Absolutely no-one on the internet knows how to implement IDiaStackWalkHelper properly. While there is a Microsoft Sample
     * (https://github.com/microsoftarchive/msdn-code-gallery-microsoft/blob/master/Visual%20Studio%20Product%20Team/Visual%20Studio%20Debug%20Engine%20Sample/%5BC%23%5D%5BC%2B%2B%5D-Visual%20Studio%20Debug%20Engine%20Sample/C%23%20and%20C%2B%2B/Microsoft.VisualStudio.Debugger.SampleEngineWorker/DiaStackWalkerHelper.cpp)
     * which gives a basic outline of how an IDiaStackWalkHelper might be implemented, absolutely everyone who has a go at implementing this theirselves
     * punts on implementing IDiaStackWalkHelper::pdataForVA() which is absolutely essential for x64 stack unwinding.
     *
     * As Microsoft's IDiaStackWalkHelper sample indicates, you may not have to implement searchForReturnAddress and searchForReturnAddressStart.
     * On x86, failing to implement these will result in DIA's default implementation msdia140!CX86StackWalkHelper::searchForReturnAddressDefault being called.
     * msdia140!CAMD64StackTrav does not have such a method, and none of the searchForReturnAddress* methods were called in my testing. On that basis
     * I am tentatively assuming these methods can be completely ignored, but I didn't stress test this enough to sure.
     *
     * When performing a stack walk, there are two separate concepts around the "frame pointer" that need to be considered. There is the frame
     * pointer as stored in the register context (EBP/RBP), and then there is the address of the "actual" frame, regardless of what value is stored in the CPU register.
     * When a given function has a RUNTIME_FUNCTION record associated with it, it is said not to be a "leaf frame". "Leaf frames" are, essentially,
     * functions that call other functions (and so modify non-volatile registers like RSP).
     *
     * When such a non-leaf function is in its epilogue, stack unwinders will simulate popping the return address of the current function off the stack, thereby increasing RSP by 8.
     *
     * It is at this point that dbghelp!StackWalkEx and other stack walkers diverge in their behavior. In dbghelp!StackWalkEx, after unwinding the
     * non-leaf frame, dbghelp!DbsX64StackUnwinder::BaseUnwind will declare that the "frame pointer" (i.e. STACKFRAME_EX.AddrFrame) should be set
     * to the stack address _before_ the address the return address was sitting in. Since it did RSP += 8 when it simulated popping the return address
     * off the stack, it does RSP - 16 to get the address to use for the AddrFrame.
     *
     * No other stack walkers I've investigated behave like this. In AMD's CodeXL library, RSP is increased by 8 when the return address is popped
     * (StackVirtualUnwinder::GetFrameData) but when the frame data is requested, it simply does RSP - 8.
     * https://github.com/jrmuizel/CodeXL/blob/master/CodeXL/Components/CpuProfiling/AMDTCpuCallstackSampling/src/StackWalker/x64/StackVirtualUnwinder.cpp#L148
     *
     * Similarly, msdia140!CAMD64StackTrav::PopRegister does RSP += 8 and msdia140!CAMD64StackTrav::get does RSP - 8. As a result, the DiaStackFrame.Base
     * does not match the STACKFRAME_EX.AddrFrame returned by DbgHelp. Does this matter? Well, a key design point of ChaosDbg is that it maintains a strong
     * level of compatibility with DbgEng. If ChaosDbg starts spitting out "off by 8" values here and there, that may make it very confusing trying to switch
     * between multiple debuggers. Trying to "fix up" DIA's returned base address also seems fraught with peril; at that point, we basically need to
     * write an x64 stack unwinder ourselves to know when it's safe to mess with the value!
     *
     * Visual Studio's IDiaStackWalkHelper is implemented in vsdebugeng.impl in Common::CDiaStackUnwindHelperBase and PDataUnwind::CDiaStackUnwindHelper::pdataForVA
     *
     * Another interesting gotcha is that CLSID_DiaSource and CLSID_DiaStackWalker share the same code path in msdia140!DllGetClassObject, which means
     * that they both set the flag that DIA is being used in COM-style string mode. Meaning, if you're using CLSID_DiaStackWalker, you cannot use
     * CLSID_DiaSourceAlt; you have to use CLSID_DiaSource.
     */

    internal unsafe class DiaStackWalkHelper : IDiaStackWalkHelper
    {
        internal IMAGE_FILE_MACHINE Machine { get; }

        protected IMemoryReader memoryReader;
        private CROSS_PLATFORM_CONTEXT* context;
        private static int pdataSize = Marshal.SizeOf<AMD64_RELOCATED_PDATA_ENTRY>();

        private DiaTryGetModuleDelegate tryGetModule;
        private DiaTryGetFunctionTableEntryDelegate tryGetFunctionTableEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiaStackWalkHelper"/> class.
        /// </summary>
        /// <param name="memoryReader">The <see cref="IMemoryReader"/> to use to interact with the target processes memory.</param>
        /// <param name="context">The current context of the thread to capture a stack trace of. This value will be mutated by the stack walker as each frame is walked.</param>
        public DiaStackWalkHelper(IMemoryReader memoryReader, CROSS_PLATFORM_CONTEXT* context, DiaTryGetModuleDelegate tryGetModule, DiaTryGetFunctionTableEntryDelegate tryGetFunctionTableEntry)
        {
            this.memoryReader = memoryReader;
            Machine = memoryReader.Is32Bit ? IMAGE_FILE_MACHINE.I386 : IMAGE_FILE_MACHINE.AMD64;
            this.context = context;
            this.tryGetModule = tryGetModule;
            this.tryGetFunctionTableEntry = tryGetFunctionTableEntry;
        }

        #region IDiaStackWalkHelper

        /* Unlike dbghelp!StackWalkEx where you pass in an initial thread context, in IDiaStackWalkHelper you store the thread context
         * in your helper implementation, and DIA calls back to IDiaStackWalkerHelper::get_registerValue and put_registerValue to read/write
         * registers in it as it desires */

        HRESULT IDiaStackWalkHelper.get_registerValue(CV_HREG_e index, out long pRetVal)
        {
            //Iced registers are our preferred register enum type,
            //which we provide extension methods on CrossPlatformContext for
            //for interacting with CROSS_PLATFORM_CONTEXT field members
            var register = index.ToIcedRegister(Machine);

            pRetVal = context->GetRegisterValue(register, Machine);

            return HRESULT.S_OK;
        }

        HRESULT IDiaStackWalkHelper.put_registerValue(CV_HREG_e index, long NewVal)
        {
            var register = index.ToIcedRegister(Machine);

            context->SetRegisterValue(register, Machine, NewVal);

            return HRESULT.S_OK;
        }

        HRESULT IDiaStackWalkHelper.readMemory(MemoryTypeEnum type, long va, int cbData, out int pcbData, byte[] pbData)
        {
            /* Microsoft's sample says that you should set pcbData to the number of bytes read +1, and that DIA may ask
             * for ridiculously large amounts of data. I didn't see anything of the sort. I figured that maybe you had to
             * set pcbData to the number of bytes read +1 to signal to DIA to ask a follow up question after all of the bytes
             * I read, but one time DIA crashed because it treated pcbData as actually being the size of pbData...and so,
             * we ignore what Microsoft's sample says and read all of the data in one go, and correctly report the buffer size. */

            //Sometimes DIA asks us to read 0 bytes. It will get upset if you respond with an error, so just respond that everything's good

            if (cbData == 0)
            {
                pcbData = 0;
                return HRESULT.S_OK;
            }

            fixed (byte* buffer = pbData)
            {
                var hr = memoryReader.ReadVirtual(va, (IntPtr) buffer, cbData, out pcbData);

                //Whatever failure you return here will likely be swallowed by DIA, and a E_DIA_FRAME_ACCESS
                //error will be returned from DiaStackWalker::GetEnumFrames/GetEnumFrames2
                if (pcbData == 0)
                {
                    if (hr != HRESULT.S_OK)
                        return hr;

                    return HRESULT.E_FAIL;
                }
            }

            return HRESULT.S_OK;
        }

        HRESULT IDiaStackWalkHelper.pdataForVA(long va, int cbData, out int pcbData, byte[] pbData)
        {
            /* If you debug vsdebugeng_impl!PDataUnwind::CDiaStackUnwinHelper::pdataforVA you'll observe that this
             * function calls vsdebugeng!dispatcher::Native::DkmNativeModuleInstance::GetFunctionTableEntry
             * Based on this, it would be reasonable to assume that all you need to do here is write a RUNTIME_FUNCTION
             * entry into pbData, and that this function essentially performs the same function as dbghelp!SymFunctionTableAccess64.
             * While this is true to an extent, there's one big gotcha: cbData will be 24 bytes, and RUNTIME_FUNCTION is only 12!
             * Fortunately, msdia140!CAMD64StackTrav::GetUnwindInfo (which will get upset when you fail to readMemory() properly
             * if you return a RUNTIME_FUNCTION from this method) gives us a clue: you're in fact meant to return a AMD64_RELOCATED_PDATA_ENTRY!
             * This type is basically the exact same thing as a RUNTIME_FUNCTION, except its 3 fields are 8 bytes wide, instead 4
             * (for a total of 3*8=24 bytes). So, simply get a RUNTIME_FUNCTION from a usual source, and then upgrade it to an
             * AMD64_RELOCATED_PDATA_ENTRY
             *
             * Since each member of AMD64_RELOCATED_PDATA_ENTRY is large enough to store a 64-bit pointer, and the UnwindData of this
             * structure will be passed straight to readMemory(), it makes sense that you should convert each RVA in your RUNTIME_FUNCTION
             * to an absolute address, which you can do by getting the base address from imageForVA() */

            pcbData = 0;

            //This should never be called for x86, and if it's called I don't know what you'd be expected to do here (something to do with FPO data maybe?)
            if (memoryReader.Is32Bit)
                return HRESULT.E_NOTIMPL;

            if (cbData != pdataSize)
                return HRESULT.E_FAIL;

            if (!tryGetModule(va, out var moduleBase, out _))
                return HRESULT.E_FAIL;

            if (!tryGetFunctionTableEntry(va, out var runtimeFunction))
            {
                //If a given function does not have a RUNTIME_FUNCTION associated with it, that means its a leaf frame (that itself doesn't call
                //any other functions). Assuming you didn't mess up your RUNTIME_FUNCTION resolution logic above, this is OK
                return HRESULT.E_FAIL;
            }

            fixed (byte* buffer = pbData)
            {
                var pEntry = (AMD64_RELOCATED_PDATA_ENTRY*) buffer;

                pEntry->BeginAddress = runtimeFunction.BeginAddress + moduleBase;
                pEntry->EndAddress = runtimeFunction.EndAddress + moduleBase;
                pEntry->UnwindData = runtimeFunction.UnwindData + moduleBase;
            }

            pcbData = pdataSize;

            return HRESULT.S_OK;
        }

        HRESULT IDiaStackWalkHelper.searchForReturnAddress(IDiaFrameData frame, out long returnAddress)
        {
            //On x86 at least, failing to implement this will result in DIA's default implementation being used instead
            returnAddress = default;
            return HRESULT.E_NOTIMPL;
        }

        HRESULT IDiaStackWalkHelper.searchForReturnAddressStart(IDiaFrameData frame, long startAddress, out long returnAddress)
        {
            //On x86 at least, failing to implement this will result in DIA's default implementation being used instead
            returnAddress = default;
            return HRESULT.E_NOTIMPL;
        }

        HRESULT IDiaStackWalkHelper.frameForVA(long va, out IDiaFrameData ppFrame)
        {
            //Microsoft's sample says this might be called in cases where optimization has messed things up, and apparently
            //what you do is do diaSession.GetTable<DiaEnumFrameData>().FrameByVA(va) (with appropriate error handling)

            if (tryGetModule(va, out var moduleBase, out var diaSession) && diaSession != null)
            {
                //Searching for symbols by their full virtual address doesn't seem to work, even if DiaSession.LoadedAddress is set.
                //As I haven't figured out how to query VA values directly yet, we'll just convert them to an RVA first

                var rva = (int) (va - moduleBase);

                var diaEnumFrameData = diaSession.GetTable<DiaEnumFrameData>();

                var hr = diaEnumFrameData.TryFrameByRVA(rva, out var frameData);

                if (hr == HRESULT.S_OK)
                    ppFrame = frameData.Raw;
                else
                    ppFrame = null;

                return hr;
            }

            ppFrame = null;
            return HRESULT.S_FALSE;
        }

        HRESULT IDiaStackWalkHelper.symbolForVA(long va, out IDiaSymbol ppSymbol)
        {
            //Not sure under what circumstances this would ever be called,
            //as DiaStackFrame objects don't provide easy access to the symbols
            //that would be associated with them. Microsoft's sample does
            //DiaSession.FindSymbolByVA(va, SymTagEnum.Function)

            //Answer: it's certainly called when walking x86 stacks

            if (tryGetModule(va, out var moduleBase, out var diaSession))
            {
                //Searching for symbols by their full virtual address doesn't seem to work, even if DiaSession.LoadedAddress is set.
                //As I haven't figured out how to query VA values directly yet, we'll just convert them to an RVA first

                var rva = (int) (va - moduleBase);

                //Microsoft's sample filters for just SymTagEnum.Function entries, however I've found that sometimes this can cause a miss when passing Null would give you a hit
                var hr = diaSession.TryFindSymbolByRVA(rva, SymTagEnum.Null, out var symbol);

                if (hr == HRESULT.S_OK)
                    ppSymbol = symbol.Raw;
                else
                    ppSymbol = null;

                return hr;
            }

            ppSymbol = null;
            return HRESULT.S_FALSE;
        }

        HRESULT IDiaStackWalkHelper.imageForVA(long vaContext, out long pvaImageStart)
        {
            //This function should basically perform the same thing as dbghelp!SymGetModuleBase64.
            //Look vaContext up in your list of loaded modules and return the module that
            //contains vaContext between its start and end address

            if (tryGetModule(vaContext, out pvaImageStart, out _))
                return HRESULT.S_OK;

            return HRESULT.E_FAIL;
        }

        HRESULT IDiaStackWalkHelper.addressForVA(long va, out int pISect, out int pOffset)
        {
            //Microsoft's sample says you can just call DiaSession.AddressforVA(),
            //but this method was never called in my x64 tests (maybe it's called in x86 stack walking)

            throw new NotImplementedException();
        }

        //These functions aren't even documented as existing on MSDN, but they do exist, and are listed
        //in dia2.h. I couldn't see any evidence of these in vsdebugeng.impl.dll, so it's possible
        //Microsoft just left them unimplemented as well and they got comdat folded

        HRESULT IDiaStackWalkHelper.numberOfFunctionFragmentsForVA(long vaFunc, int cbFunc, out int pNumFragments)
        {
            throw new NotImplementedException();
        }

        HRESULT IDiaStackWalkHelper.functionFragmentsForVA(long vaFunc, int cbFunc, int cFragments, out long pVaFragment, out int pLenFragment)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
