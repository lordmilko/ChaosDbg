using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ChaosDbg.Disasm;
using ChaosDbg.Symbol;
using ChaosLib.Metadata;
using ClrDebug;
using Iced.Intel;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for resolving managed addresses to symbol identifiers when disassembling managed code.
    /// </summary>
    public class CordbDisasmSymbolResolver : IIndirectSymbolResolver
    {
        private CordbProcess process;

        //If all we got was "this value exists inside some module", this is very hard to read through when we get a spew of disassembly. Thus, we apply a different text kind to things that actually represent "real" symbols
        private readonly FormatterTextKind FullSymbolKind = FormatterTextKind.Label;
        private readonly FormatterTextKind NoisySymbolKind = FormatterTextKind.LabelAddress;

        /// <summary>
        /// Gets or sets the disassembler to use for resolving indirect symbol addresses.
        /// </summary>
        public INativeDisassembler ProcessDisassembler { get; set; }

        public CordbDisasmSymbolResolver(CordbProcess process)
        {
            this.process = process;
        }

        public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize, out SymbolResult symbol)
        {
            //Partially based on dnSpy ClrDacImpl.cs!TryGetSymbolCore and SOS disasmX86.cpp!HandleValue

            //Fast path out tiny addresses, or addresses so big they're clearly negative numbers
            if (address < 0x10_000 || ((long) address) < 0)
            {
                symbol = default;
                return false;
            }

            symbol = default;

            if (instructionOperand != -1)
            {
                switch (instruction.GetOpKind(operand))
                {
                    case OpKind.Immediate8to32:
                        if (((int) address) == -1)
                            return false;

                        break;

                    case OpKind.Immediate32:
                        if ((int) address == int.MaxValue) //0x7fffffff
                            return false;

                        break;
                }
            }

            if (instruction.MemoryBase is Register.EIP or Register.RIP)
            {
                /* Calls to managed methods have a tendancy to be made via indirect calls. e.g.
                 *
                 * System.Management.Automation.PSTraceSource.GetNewTraceSource
                 *     call    qword ptr [System_Management_Automation+0x11cc268 (00007ffc`1167c268)] (System.String.IsNullOrEmpty(System.String))
                 *
                 * If you attempt to resolve the address that the indirect call is being made against, you'll tend to resolve a symbol with a large displacement belonging
                 * to the _calling_ function. But the whole point of this operation is that we want to resolve a symbol belonging to a callee! Thus, we say that when we're
                 * trying to resolve an indirect call, we ONLY allow processing native symbols. While we would ideally like to show the symbols belonging to the indirection
                 * address as well, it results in such a big spew of symbols (that may have a '+' sign in them themselves) that it's quite hard to see that this symbol being
                 * listed ISN'T actually the target of the call, but just the base of a very large displacement. */

                if (process.Symbols.TrySymFromAddr((long) address, SymFromAddrOption.Fallback | SymFromAddrOption.CLR, out var displacedSymbol))
                {
                    symbol = MakeSymbol(displacedSymbol);
                    return true;
                }
            }
            else
            {
                //No indirection. Allow asking for managed symbols

                var isValidAddress = TestIsValidAddress(address);

                /* Most SOS introspection methods have a tendancy to just blast ahead assuming the address we passed is valid, and then throwing an exception when it turns out
                 * the passed address isn't valid after all. This really slows down the performance of trying to resolve symbols for every single random operand that may be a pointer
                 * to something. To mitigate this, we split our symbol resolution logic up into tiers, so that methods that are guaranteed not to throw can be tried before the more
                 * spray and pray ones.
                 *
                 * Note that if we can immediately see that the address isn't even valid, don't even bother trying to execute lookups that may throw. It's just
                 * a waste of time (and performance) */

                //Just do safe, non-indirecting lookups to start with
                var options = SymFromAddrOption.Safe & ~SymFromAddrOption.Thunk;

                //We'll risk doing dangerous checks if the address is at least valid
                if (isValidAddress)
                    options |= SymFromAddrOption.DangerousManaged;

                if (process.Symbols.TrySymFromAddr((long) address, options, out var displacedSymbol))
                {
                    //Don't use full symbol kind when it's just a conditional jump (indicating we're likely going somewhere else inside the current method). This makes the output easier to read
                    symbol = MakeSymbol(displacedSymbol, instruction.FlowControl == FlowControl.ConditionalBranch);
                    return true;
                }
            }

            return false;
        }

        private bool TestIsValidAddress(ulong address)
        {
            //Doesn't matter whether it's managed or not
            var hr = process.DataTarget.TryReadVirtual<byte>(address, out _);

            return hr == S_OK;
        }

        public bool TryGetIndirectSymbol(in Instruction instruction, ulong address, int addressSize, out ulong targetAddress, out SymbolResult symbol)
        {
            //Fast path out tiny addresses
            if (address < 0x1000)
            {
                targetAddress = default;
                symbol = default;
                return false;
            }

            if (TryReadPointer(address, addressSize, out targetAddress))
            {
                var isValidAddress = TestIsValidAddress(targetAddress);

                //If it's an unmanaged stub, don't try and resolve it. The CLR gives a super generic name for thunks, and obviously anything that throws is going to be a waste of time
                var options = SymFromAddrOption.Safe & ~SymFromAddrOption.Thunk;

                if (isValidAddress)
                    options |= SymFromAddrOption.DangerousManaged;

                if (process.Symbols.TrySymFromAddr((long) targetAddress, options, out var displacedSymbol))
                {
                    symbol = MakeSymbol(displacedSymbol);
                    return true;
                }

                //Does the address point to a thunk that then calls into the CLR?
                if (TryGetThunk(instruction, targetAddress, out var thunkAddress, out symbol))
                {
                    targetAddress = thunkAddress;
                    return true;
                }

                //Try and resolve native symbols last. When an address does lie inside a module, we'll obviously get a fallback symbol, but we want to prefer
                //retrieving thunk information first if it is available
                if (process.Symbols.TrySymFromAddr((long) targetAddress, SymFromAddrOption.Fallback, out displacedSymbol))
                {
                    symbol = MakeSymbol(displacedSymbol);
                    return true;
                }
            }

            targetAddress = default;
            symbol = default;

            return false;
        }

        /// <summary>
        /// Tries to read an indirect memory address, as might be found in a call [address] or jmp [address] instruction.
        /// </summary>
        /// <param name="address">The pointer address that should be read.</param>
        /// <param name="addressSize">The size of <see cref="address"/> (4 in x86, 8 in x64).</param>
        /// <param name="result">The value pointed to by <see cref="address"/>.</param>
        /// <returns>True if the pointer was successfully read, otherwise false.</returns>
        public bool TryReadPointer(ulong address, int addressSize, out ulong result)
        {
            result = 0;

            switch (addressSize)
            {
                case 4:
                    //Doesn't need to be a managed read
                    if (process.DataTarget.TryReadVirtual<uint>(address, out var raw) == S_OK)
                    {
                        result = raw;
                        return true;
                    }

                    return false;

                case 8:
                    //Doesn't need to be a managed read
                    return process.DataTarget.TryReadVirtual(address, out result) == S_OK;

                default:
                    throw new NotImplementedException($"Don't know how to read pointer of size {address}");
            }
        }

        #region Thunk

        private bool TryGetThunk(in Instruction instruction, ulong address, out ulong targetAddress, out SymbolResult symbol)
        {
            /* When debugging pwsh.exe we'll sometimes get a call pointing to a strange thunk that looks like this:
             *
             * Microsoft_PowerShell_ConsoleHost+0x5709c:
             *     00007ffc`5cfa709c 33c0            xor     eax,eax
             *     00007ffc`5cfa709e 6a02            push    2
             *     00007ffc`5cfa70a0 ff352a1b0300    push    qword ptr [Microsoft_PowerShell_ConsoleHost+0x88bd0 (00007ffc`5cfd8bd0)]
             *     00007ffc`5cfa70a6 ff253c1b0300    jmp     qword ptr [Microsoft_PowerShell_ConsoleHost+0x88be8 (00007ffc`5cfd8be8)]
             *
             * This is a ReadyToRun import thunk
             *  https://github.com/dotnet/runtime/blob/79dd9bae9bb881eb716b608577c4cedc6c9cba72/src/coreclr/tools/aot/ILCompiler.ReadyToRun/Compiler/DependencyAnalysis/ReadyToRun/Target_X86/ImportThunk.cs
             *  https://github.com/dotnet/runtime/blob/79dd9bae9bb881eb716b608577c4cedc6c9cba72/src/coreclr/tools/aot/ILCompiler.ReadyToRun/Compiler/DependencyAnalysis/ReadyToRun/Target_X64/ImportThunk.cs
             *
             * Many (all?) of these thunks appear to be specific to .NET Core
             */

            if (instruction.Mnemonic == Mnemonic.Call && ProcessDisassembler != null)
            {
                var original = ProcessDisassembler.IP;

                try
                {
                    if (instruction.CodeSize == CodeSize.Code64)
                    {
                        if (TryGetThunk64((long) address, out targetAddress, out symbol))
                            return true;
                    }
                    else
                    {
                        if (TryGetThunk32((long) address, out targetAddress, out symbol))
                            return true;
                    }

                }
                finally
                {
                    ProcessDisassembler.IP = original;
                }
            }

            targetAddress = default;
            symbol = default;
            return false;
        }

        private bool TryGetThunkSymbol(ulong address, out ulong targetAddress, out SymbolResult result)
        {
            //Try helpers first, then native

            ulong displacement = 0;

            if (process.Symbols.TrySymFromAddr((long) address, SymFromAddrOption.Managed | SymFromAddrOption.Native, out var symbol))
            {
                targetAddress = address;
                result = MakeSymbol(targetAddress, displacement, $"Thunk -> {symbol.Symbol}");
                return true;
            }

            targetAddress = default;
            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ThunkFailure()
        {
            Debug.Assert(false, "Encountered what may be a thunk in a format we don't know how to handle");
            return false;
        }

        #region x86

        private bool TryGetThunk32(long address, out ulong targetAddress, out SymbolResult result)
        {
            targetAddress = default;
            result = default;

            return ThunkFailure();
        }

        #endregion
        #region x64

        private bool TryGetThunk64(long address, out ulong targetAddress, out SymbolResult result)
        {
            targetAddress = default;
            result = default;

            var first = ProcessDisassembler.Disassemble(address);

            var firstIced = first.Instruction;

            if (firstIced.Mnemonic == Mnemonic.Mov)
            {
                if (firstIced.Op0Register == Register.RAX && firstIced.Op1Register == Register.R11)
                    return TryGetR2RVirtualStubDispatch(out targetAddress, out result);

                return TryGetAllocThunk64(out targetAddress, out result);
            }

            if (firstIced.Mnemonic == Mnemonic.Xor)
                return TryGetR2RDelayLoadThunk64(firstIced, out targetAddress, out result);

            return false;
        }

        private bool TryGetAllocThunk64(out ulong targetAddress, out SymbolResult result)
        {
            targetAddress = default;
            result = default;

            var second = ProcessDisassembler.Disassemble();

            var secondIced = second.Instruction;

            if (secondIced.Mnemonic == Mnemonic.Jmp && secondIced.FlowControl == FlowControl.UnconditionalBranch)
            {
                //CORINFO_HELP_NEWSFAST: allocator for small, non-finalizer, non-array object

                if (secondIced.Op0Kind != OpKind.NearBranch64)
                    return ThunkFailure();

                if (TryGetThunkSymbol(secondIced.MemoryDisplacement64, out targetAddress, out result))
                    return true;
            }
            else if (secondIced.Mnemonic == Mnemonic.Mov)
            {
                //e.g.
                //CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE
                //CORINFO_HELP_NEWARR_1_OBJ

                var third = ProcessDisassembler.Disassemble();

                var thirdIced = third.Instruction;

                if (thirdIced.Mnemonic != Mnemonic.Jmp || thirdIced.FlowControl != FlowControl.UnconditionalBranch)
                    return false;

                switch (thirdIced.Op0Kind)
                {
                    case OpKind.NearBranch64:
                        if (TryGetThunkSymbol(thirdIced.NearBranch64, out targetAddress, out result))
                            return true;

                        break;

                    default:
                        return ThunkFailure();
                }
            }

            return false;
        }

        private bool TryGetR2RDelayLoadThunk64(in Instruction firstIced, out ulong targetAddress, out SymbolResult result)
        {
            //Looking for a x64 ReadyToRun DelayLoadHelper thunk
            targetAddress = default;
            result = default;

            //https://github.com/dotnet/runtime/blob/79dd9bae9bb881eb716b608577c4cedc6c9cba72/src/coreclr/tools/aot/ILCompiler.ReadyToRun/Compiler/DependencyAnalysis/ReadyToRun/Target_X64/ImportThunk.cs

            //xor eax,eax
            if (firstIced.Op0Register != Register.EAX || firstIced.Op1Register != Register.EAX)
                return false;

            var second = ProcessDisassembler.Disassemble();

            //push table index
            if (second.Instruction.Mnemonic != Mnemonic.Push)
                return false;

            var third = ProcessDisassembler.Disassemble();

            //push [module]
            if (third.Instruction.Mnemonic != Mnemonic.Push)
                return false;

            var fourth = ProcessDisassembler.Disassemble();

            var fourthIced = fourth.Instruction;

            if (fourthIced.Mnemonic == Mnemonic.Jmp && fourthIced.FlowControl == FlowControl.IndirectBranch && fourthIced.Op0Kind == OpKind.Memory && fourthIced.MemoryBase == Register.RIP)
            {
                if (TryReadPointer(fourthIced.MemoryDisplacement64, 8, out var target))
                {
                    if (TryGetThunkSymbol(target, out targetAddress, out result))
                        return true;
                }
            }

            return false;
        }

        private bool TryGetR2RVirtualStubDispatch(out ulong targetAddress, out SymbolResult result)
        {
            targetAddress = default;
            result = default;

            //https://github.com/dotnet/runtime/blob/79dd9bae9bb881eb716b608577c4cedc6c9cba72/src/coreclr/tools/aot/ILCompiler.ReadyToRun/Compiler/DependencyAnalysis/ReadyToRun/Target_X64/ImportThunk.cs#L54C27-L54C46

            //push table index or push [module]
            var second = ProcessDisassembler.Disassemble();

            if (second.Instruction.Mnemonic != Mnemonic.Push)
                return false;

            //push [module] or jmp
            var third = ProcessDisassembler.Disassemble();

            var jmp = third;

            switch (third.Instruction.Mnemonic)
            {
                case Mnemonic.Push:
                    jmp = ProcessDisassembler.Disassemble();
                    break;

                case Mnemonic.Jmp:
                    break;

                default:
                    return false;
            }

            var icedJmp = jmp.Instruction;

            if (icedJmp.FlowControl == FlowControl.IndirectBranch)
            {
                if (TryGetThunkSymbol(icedJmp.MemoryDisplacement64, out targetAddress, out result))
                    return true;
            }

            return ThunkFailure();
        }

        #endregion
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SymbolResult MakeSymbol(ulong address, ulong displacement, string name)
        {
            //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
            //is different from the input address and add +0x100 to the resulting output
            return new SymbolResult(address - displacement, name, FormatterTextKind.Label);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SymbolResult MakeSymbol(IDisplacedSymbol displacedSymbol, bool? shouldReduceAttention = null)
        {
            //A "noisy" symbol is something that is like foo.exe+0x1000 (not very helpful) or in some cases other types of instructions such as conditional jumps (we don't need to draw attention to them)

            //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
            //is different from the input address and add +0x100 to the resulting output
            return new SymbolResult(
                (ulong) displacedSymbol.Address - (ulong) displacedSymbol.Displacement,
                displacedSymbol.Symbol.ToString(),
                displacedSymbol is DisplacedMissingSymbol || shouldReduceAttention == true ? NoisySymbolKind : FullSymbolKind
            );
        }
    }
}
