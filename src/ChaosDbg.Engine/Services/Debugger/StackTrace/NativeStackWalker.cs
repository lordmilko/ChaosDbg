using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for performing a native stack walk in a similar manner to DbgEng.
    /// </summary>
    class NativeStackWalker : IDisposable
    {
        private CLRDataTarget dataTarget;
        private DbgHelpSession dbgHelpSession;
        private DynamicFunctionTableProvider dynamicFunctionTableProvider;
        private IntPtr dynamicFunctionTableResult;

        public NativeStackWalker(
            ICLRDataTarget dataTarget,
            DbgHelpSession dbgHelpSession)
        {
            if (dataTarget == null)
                throw new ArgumentNullException(nameof(dataTarget));

            this.dataTarget = new CLRDataTarget(dataTarget);
            this.dbgHelpSession = dbgHelpSession;

            /* I can't believe how long I spent trying to figure out why StackWalkEx was giving me different results
             * to what DbgEng yields, only to stumble upon this post from Microsoft after I had finally emerged from
             * this rabbit hole https://github.com/dotnet/runtime/issues/57962#issuecomment-908778223
             *
             * In this issue, Microsoft also make mention of additional logic encoded in DbgEng pertaining to mscordaccore.
             * It appears this special logic may only relate to minidumps; I tried to debug WinDbg opening a minidump and couldn't
             * get it to hit the code path that checks for mscordaccore (dbgeng!Win32LiveSystemProvider::CheckForAuxProvider),
             * so I'm ignoring that issue for now */
            if (this.dataTarget.MachineType != IMAGE_FILE_MACHINE.I386)
            {
                dynamicFunctionTableProvider = new DynamicFunctionTableProvider(dataTarget);
                dbgHelpSession.FunctionEntryCallback = DynamicFunctionCallback;
            }
        }

        private IntPtr DynamicFunctionCallback(IntPtr hProcess, long AddrBase, long UserContext)
        {
            /* When attempting to resolve function table addresses (required for unwinding dynamically generated 64-bit
             * code in Windows) for some insane reason you're expected to return a _pointer_ to  RUNTIME_FUNCTION object.
             * So how are you suppose to handle releasing the entity! DbgEng handles this by declaring static variables
             * that can then hang around (I suppose still on the stack) after the function returns. That is not a valid
             * solution for us; so we must take care of managing a reusable block of memory that eventually gets disposed
             * ourselves. */

            if (dynamicFunctionTableProvider.TryGetDynamicFunctionEntry(hProcess, AddrBase, out var match))
            {
                if (dynamicFunctionTableResult == IntPtr.Zero)
                    dynamicFunctionTableResult = Marshal.AllocHGlobal(Marshal.SizeOf<RUNTIME_FUNCTION>());
                else
                    Marshal.DestroyStructure(dynamicFunctionTableResult, typeof(RUNTIME_FUNCTION));

                Marshal.StructureToPtr(match, dynamicFunctionTableResult, false);

                return dynamicFunctionTableResult;
            }

            return IntPtr.Zero;
        }

        public IEnumerable<NativeFrame> Walk(
            IntPtr hProcess,
            IntPtr hThread,
            CrossPlatformContext context,
            Func<NativeFrame, bool> predicate = null)
        {
            var stackFrame = new STACKFRAME_EX
            {
                StackFrameSize = Marshal.SizeOf<STACKFRAME_EX>()
            };

            var machineType = dataTarget.MachineType;

            var raw = context.Raw;

            while (true)
            {
                //StackWalKEx may modify the CONTEXT record. We want to capture this modified CONTEXT to store in the NativeFrame,
                //but we can't use unsafe code in an iterator, so we're forced to play games, wrapping the unsafe code up in an internal method
                unsafe bool DoStackWalk(ref CROSS_PLATFORM_CONTEXT ctx)
                {
                    fixed (CROSS_PLATFORM_CONTEXT* ptr = &ctx)
                    {
                        return DbgHelp.Native.StackWalkEx(
                            machineType,
                            hProcess,
                            hThread,
                            ref stackFrame,
                            (IntPtr) ptr,
                            null,
                            FunctionTableAccess,
                            GetModuleBase,
                            null,
                            SYM_STKWALK.DEFAULT
                        );
                    }
                }

                var result = DoStackWalk(ref raw);

                if (!result)
                    break;

                dbgHelpSession.TrySymFromAddr(stackFrame.AddrPC.Offset, out var symbol);

                string moduleName = null;

                if (dbgHelpSession.TrySymGetModuleInfo64(stackFrame.AddrPC.Offset, out var moduleInfo) == S_OK)
                    moduleName = Path.GetFileNameWithoutExtension(moduleInfo.ImageName);

                var newFrame = new NativeFrame(stackFrame, symbol, moduleName, new CrossPlatformContext(context.Flags, raw));

                if (predicate != null && !predicate(newFrame))
                    yield break;

                yield return newFrame;
            }
        }

        private unsafe IntPtr FunctionTableAccess(IntPtr hProcess, long addrBase)
        {
            /* DbgEng employs the following logic in SwFunctionTableAccess():
             * - if the target is i386, it tries to resolve the function table via SymFunctionTableAccess()
             *   and then applies fixes to the resulting FPO_DATA* structure for a few edge naughty edge cases, or
             *   even synthesizes the FPO_DATA structure out of nothing if one can't be found!
             * - If the target is AMD64, calls ProcessInfo::FindImageByOffset() and, if a result was found
             *   checks that ImageInfo::GetMachineType(result) does not retuen i386 - which includes some checks
             *   pertaining to the CLR. If it is i386, a function table address of 0 is returned
             * - Otherwise, we fallback to calling SymFunctionTableAccess64()
             *
             * At this initial stage, handling all these corner cases is too much effort. This can be revisited in
             * the future when some specific stack trace issues arise */

            //Just try and resolve the address based on the modules that are known to DbgEng (which should be all modules,
            //since we should be informing DbgEng whenever modules are loaded when doing interop debugging). If we're trying
            //to resolve the function table of an address in a CLR method, this will occur in the DynamicFunctionCallback()
            //registered above after DbgEng's normal resolution logic fails
            return DbgHelp.Native.SymFunctionTableAccess64(hProcess, addrBase);
        }

        private long GetModuleBase(IntPtr hProcess, long qwAddr)
        {
            //DbgEng searches the modules it knows about, and if no module is found, defers to querying for dynamic function tables directly.
            //All modules we know about should be known to DbgHelp as well, so we're fine to just call SymGetModuleBase64() directly

            var result = DbgHelp.Native.SymGetModuleBase64(hProcess, qwAddr);

            if (result != 0)
                return result;

            //Dynamic function tables only apply to 64-bit processes, so our resolver won't be initialized
            //when we're targeting an i386 process.
            if (dynamicFunctionTableProvider != null)
            {
                //Maybe this address is within a dynamically generated function
                if (dynamicFunctionTableProvider.TryGetDynamicFunctionTableModuleBase(qwAddr, out result))
                    return result;
            }

            //Failed to resolve the specified address
            return 0;
        }

        public void Dispose()
        {
            if (dynamicFunctionTableResult != IntPtr.Zero)
            {
                Marshal.DestroyStructure(dynamicFunctionTableResult, typeof(RUNTIME_FUNCTION));
                Marshal.FreeHGlobal(dynamicFunctionTableResult);
            }

            if (dynamicFunctionTableProvider != null)
            {
                dynamicFunctionTableProvider.Dispose();
                dbgHelpSession.FunctionEntryCallback = null;
            }
        }
    }
}
