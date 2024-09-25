using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for performing a native stack walk in a similar manner to DbgEng.
    /// </summary>
    class NativeStackWalker : IDisposable
    {
        private CLRDataTarget dataTarget;
        private INativeStackWalkerFunctionTableProvider functionTables;
        private DynamicFunctionTableProvider dynamicFunctionTableProvider;
        private IntPtr dynamicFunctionTableResult;
        private Func<long, INLINE_FRAME_CONTEXT, (IDisplacedSymbol symbol, ISymbolModule module)> getSymbol;

        public NativeStackWalker(
            ICLRDataTarget dataTarget,
            INativeStackWalkerFunctionTableProvider functionTables,
            DynamicFunctionTableCache dynamicFunctionTableCache,
            Func<long, INLINE_FRAME_CONTEXT, (IDisplacedSymbol, ISymbolModule)> getSymbol)
        {
            if (dataTarget == null)
                throw new ArgumentNullException(nameof(dataTarget));

            this.dataTarget = new CLRDataTarget(dataTarget);
            this.functionTables = functionTables;

            //While our DbgHelpSession can retrieve modules for us, we want to retrieve enhanced ISymbol/ISymbolModule instances.
            //If we already have an ISymbolModule stored somewhere else (which is likely in a debugger), use those existing instances
            //instead
            this.getSymbol = getSymbol;

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
                dynamicFunctionTableProvider = new DynamicFunctionTableProvider(dataTarget, dynamicFunctionTableCache);
                functionTables.FunctionEntryCallback = DynamicFunctionCallback;
            }
        }

        /// <summary>
        /// Represents the entry point for resolving dynamic function entry items via DbgHelp.
        /// </summary>
        /// <param name="hProcess">The process that the function entry should be resolved for.</param>
        /// <param name="AddrBase">The address to resolve.</param>
        /// <param name="UserContext">The user context that was set when this callback was resolved.</param>
        /// <returns>If <paramref name="AddrBase"/> was resolved to a dynamic function entry, a pointer to the resolved <see cref="RUNTIME_FUNCTION"/> entry. Otherwise, <see cref="IntPtr.Zero"/>.</returns>
        private IntPtr DynamicFunctionCallback(IntPtr hProcess, long AddrBase, long UserContext)
        {
            /* When attempting to resolve function table addresses (required for unwinding dynamically generated 64-bit
             * code in Windows) for some insane reason you're expected to return a _pointer_ to a RUNTIME_FUNCTION object.
             * So how are you supposed to handle releasing the entity! DbgEng handles this by declaring static variables
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

                Log("    [DynamicFunctionTable] Resolved {0:X} to {1:X}", AddrBase, match.BeginAddress);

                return dynamicFunctionTableResult;
            }

            Log("    [DynamicFunctionTable] Failed to resolve {0:X}", AddrBase);

            return IntPtr.Zero;
        }

        public IEnumerable<NativeFrame> Walk(
            IntPtr hThread,
            CrossPlatformContext context,
            Func<NativeFrame, bool> predicate = null)
        {
            /* It's very important that the address mode be set to AddrModeFlat. While you _can_ get valid stack traces without
             * specifying this, an ADDRESS_MODE of 0 signifies "AddrMode1616", which relates to translating 16-bit stacks.
             * AddrModeFlat is also listed as being the only mode supported by the library. If you don't specify AddrModeFlat,
             * you can run into issues wherein when trying to do a stack trace after ntdll has loaded but before the process has
             * fully initialized, you won't get proper stack trace results. We don't need to specify the segment, that only applies
             * to 16-bit code. */
            var stackFrame = new STACKFRAME_EX
            {
                StackFrameSize = Marshal.SizeOf<STACKFRAME_EX>(),
                AddrPC    = { Offset = context.IP, Mode = ADDRESS_MODE.AddrModeFlat },
                AddrFrame = { Offset = context.BP, Mode = ADDRESS_MODE.AddrModeFlat },
                AddrStack = { Offset = context.SP, Mode = ADDRESS_MODE.AddrModeFlat }
            };

            var machineType = dataTarget.MachineType;

            var raw = context.Raw;

            long? previousReturn = null;

            while (true)
            {
                //StackWalKEx may modify the CONTEXT record. We want to capture this modified CONTEXT to store in the NativeFrame,
                //but we can't use unsafe code in an iterator, so we're forced to play games, wrapping the unsafe code up in an internal method
                unsafe bool DoStackWalk(ref CROSS_PLATFORM_CONTEXT ctx, ref STACKFRAME_EX frame)
                {
                    fixed (CROSS_PLATFORM_CONTEXT* pCtx = &ctx)
                    fixed (STACKFRAME_EX* pFrame = &frame)
                    {
                        Log(
                            "[Begin] AddrPC: {0:X}, AddrFrame: {1:X}, AddrStack: {2:X}, AddrReturn: {3:X}, RIP: {4:X}, RSP: {5:X}, RBP: {6:X}",
                            frame.AddrPC.Offset,
                            frame.AddrFrame.Offset,
                            frame.AddrStack.Offset,
                            frame.AddrReturn.Offset,
                            ctx.GetIP(machineType),
                            ctx.GetSP(machineType),
                            ctx.GetBP(machineType)
                        );

                        var dbgHelp = functionTables.UnsafeGetDbgHelp();

                        var walkResult = dbgHelp.StackWalkEx(
                            machineType,
                            hThread,
                            (IntPtr) pFrame,
                            (IntPtr) pCtx,
                            FunctionTableAccess,
                            getModuleBaseRoutine,
                            null,
                            SYM_STKWALK.DEFAULT
                        );

                        Log(
                            "[End]   AddrPC: {0:X}, AddrFrame: {1:X}, AddrStack: {2:X}, AddrReturn: {3:X}, RIP: {4:X}, RSP: {5:X}, RBP: {6:X}",
                            frame.AddrPC.Offset,
                            frame.AddrFrame.Offset,
                            frame.AddrStack.Offset,
                            frame.AddrReturn.Offset,
                            ctx.GetIP(machineType),
                            ctx.GetSP(machineType),
                            ctx.GetBP(machineType)
                        );

                        return walkResult;
                    }
                }

                var result = DoStackWalk(ref raw, ref stackFrame);

                if (!result)
                    break;

                //Get the symbol first to aid in debugging the asserts below
                var symbolResult = getSymbol(stackFrame.AddrPC.Offset, stackFrame.InlineFrameContext);

                //We don't currently track the return address of a frame, because I think it should be implied
                //that the return address of frame is the IP of the next frame. If that isn't the case though, we need
                //to re-evaluate things
                if (previousReturn != null && previousReturn != 0)
                    Debug.Assert(previousReturn.Value == stackFrame.AddrPC.Offset, "Previous return value did not match frame offset. Is this a bug or not?");

                //In the case of an inline frame, the return address will be the same as the frame after it that it's actually
                //a part of
                if (!stackFrame.InlineFrameContext.FrameType.HasFlag(STACK_FRAME_TYPE.STACK_FRAME_TYPE_INLINE))
                    previousReturn = stackFrame.AddrReturn.Offset;

                var newFrame = new NativeFrame(stackFrame, symbolResult.symbol, symbolResult.module, new CrossPlatformContext(context.Flags, raw));

                if (predicate != null && !predicate(newFrame))
                    yield break;

                yield return newFrame;
            }
        }

        private unsafe IntPtr FunctionTableAccess(IntPtr dbgHelpId, long addrBase)
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

            //Just try and resolve the address based on the modules that are known to DbgHelp (which should be all modules,
            //since we should be informing DbgHelp whenever modules are loaded when doing interop debugging). If we're trying
            //to resolve the function table of an address in a CLR method, this will occur in the DynamicFunctionCallback()
            //registered above after DbgHelp's normal resolution logic fails

            var result = functionTables.GetFunctionTableEntry(addrBase);

            if (result == default)
                Log("    [FunctionTable] Failed to resolve {0:X}", addrBase);
            else
            {
                var rt = (RUNTIME_FUNCTION*) result;
                Log("    [FunctionTable] Resolved {0:X} to {1:X}", addrBase, rt->BeginAddress);
            }

            return result;
        }

        private long GetModuleBase(IntPtr hProcess, long qwAddr)
        {
            //DbgHelp searches the modules it knows about, and if no module is found, defers to querying for dynamic function tables directly.
            //All modules we know about should be known to DbgHelp as well, so we're fine to just call SymGetModuleBase64() directly

            if (functionTables.TryGetNativeModuleBase(qwAddr, out var moduleBase))
            {
                Log("    [ModuleBase]    Resolved {0:X} to {1:X}", qwAddr, moduleBase);
                return moduleBase;
            }

            //Dynamic function tables only apply to 64-bit processes, so our resolver won't be initialized
            //when we're targeting an i386 process.
            if (dynamicFunctionTableProvider != null)
            {
                //Maybe this address is within a dynamically generated function
                if (dynamicFunctionTableProvider.TryGetDynamicFunctionTableModuleBase(qwAddr, out var result))
                {
                    Log("    [DynamicModuleBase]    Resolved {0:X} to {1:X}", qwAddr, moduleBase);
                    return result;
                }
            }

            //Failed to resolve the specified address
            Log("    [ModuleBase]    Failed to resolve {0:X}", qwAddr);
            return 0;
        }

        [Conditional("DEBUG")]
        private void Log(string messageTemplate, params object[] propertyValues)
        {
            //Comment/uncomment this logging as required for debugging stack tracing behavior

            //ChaosLib.Log.Debug<NativeStackWalker>(messageTemplate, propertyValues);
            //Debug.WriteLine(messageTemplate, propertyValues);
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
                functionTables.FunctionEntryCallback = null;
            }
        }
    }
}
