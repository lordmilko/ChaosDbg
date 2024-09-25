using System;
using System.Collections.Generic;
using System.Linq;
using ChaosLib;
using ChaosLib.Symbols;
using ClrDebug;
using ClrDebug.DbgEng;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for enumerating the stack frames of a <see cref="CordbThread"/>.
    /// </summary>
    internal partial class CordbFrameEnumerator
    {
        //The mimimum we need for stack walking is IP, SP and BP. There is a big gotcha in AMD64: you also need to include CONTEXT_INTEGER if you want to get Rbp!
        private static ContextFlags GetContextFlags(IMAGE_FILE_MACHINE machineType) => machineType == IMAGE_FILE_MACHINE.I386 ? ContextFlags.X86ContextControl : ContextFlags.AMD64ContextControl | ContextFlags.AMD64ContextInteger;

        private static (IDisplacedSymbol symbol, ISymbolModule module) GetModuleSymbol(long addr, INLINE_FRAME_CONTEXT inlineFrameContext, CordbProcess process)
        {
            /* In order to use fancy ISymbol objects, we need an ISymbolModule (which ISymbol objects depend on).
             * We already have ISymbolModule objects saved on the modules maintained by our debugger, so retrieve
             * those instead of creating duplicate instances */

            if (addr == 0)
                return default;

            if (process.Modules.TryGetModuleForAddress(addr, out var module))
            {
                if (module is CordbNativeModule m)
                {
                    IDisplacedSymbol symbol;

                    //I suppose it's possible to have an assembly with absolutely no symbols in it?

                    if (inlineFrameContext.FrameType.HasFlag(STACK_FRAME_TYPE.STACK_FRAME_TYPE_INLINE))
                        symbol = m.SymbolModule!.GetInlineSymbolFromAddress(addr, inlineFrameContext.ContextValue);
                    else
                        symbol = m.SymbolModule!.GetSymbolFromAddress(addr);

                    return (symbol, m.SymbolModule);
                }

                //If a module is not native, it should be managed. We don't want to resolve managed symbols at this point in time; we'll display managed symbols when we
                //merge our native stack trace with a managed stack trace
                return default;
            }
            else
            {
                /* We don't know what module it's for. Possibilities include:
                 * - it's a module that we haven't received a native load event for yet
                 * - it's a module that _was_ loaded, but was unloaded after we broke into the debugger (which can happen at any time when interop debugging)
                 * - it's part of the CLR (e.g. a helper frame or a P/Invoke transition stub)
                 *
                 * Let's just try and resolve something and see how we go */

                //Whatever it is, it's not native or module addressed (both of these require knowing what the module is, which we don't!)
                var options = SymFromAddrOption.All & ~(SymFromAddrOption.Native | SymFromAddrOption.ModuleAddressed);

                if (process.Symbols.TryGetSymbolFromAddress(addr, options, out var symbol))
                {
                    return (symbol, symbol.Module);
                }

                //We don't know what this address is
                return default;
            }
        }

        /// <summary>
        /// Implements a stack frame enumerator compatible with .NET Framework 4+
        /// </summary>
        public static class V3
        {
            /// <summary>
            /// Enumerates all frames in a thread that at some point was executing managed code.<para/>
            /// If interop debugging, includes any native frames in the call stack.
            /// </summary>
            /// <param name="accessor">The accessor that is used to interact with the managed thread.</param>
            /// <param name="nativeStackWalkerKind">Specifies the native stack walking engine that should be used for unwinding frames.</param>
            /// <returns>A list of all frames in the stack trace.</returns>
            public static CordbFrame[] Enumerate(CordbThread.ManagedAccessor accessor, NativeStackWalkerKind nativeStackWalkerKind)
            {
                /* Interacting with threads is an inherently dangerous affair. Threads can decide to end at any time,
                 * even when the managed process is broken into. We can try and query whether or not the thread is currently alive,
                 * however DAC data gets cached - and this data could immediately become stale one second later! To protect against this,
                 * we forcefully flush the DAC any time we try and query whether or not a thread is alive. There is still inherently
                 * a race however; the thread could potentially decide to die after we queried it! There isn't really anything we
                 * can do about this.
                 *
                 * This method implements the results of my painstaking research, as follows
                 * - The V3 (.NET Framework 4.0) stack walking API is used instead of V2. Through some magical mechanism, the V3 API is
                 *   able to identify the context that is associated with each frame; this doesn't work in the V2 API, as all frames within
                 *   a given chain share the same context
                 *
                 * - Native frames are correctly identified and unwound. In the V3 stack walker, there are two scenarios in which you can
                 *   encounter a native frame: when you have a CorDebugNativeFrame, and when CorDebugStackWalk::GetFrame returns S_FALSE.
                 *   My observation is that in the V3 stack walker, stubs that would be represented as internal frames in V2 instead are represented
                 *   as native frames in V3. This is further supported by the fNoMetadata field in struct DebuggerIPCE_STRData which says
                 *
                 *       "Some dynamic methods such as IL stubs and LCG methods don't have any metadata. This is used only by the V3 stackwalker, not the V2
                 *       one, because we only expose dynamic methods as real stack frames in V3."
                 *
                 *   When we get these metadataless IL stubs, they occur in a chain where IsManaged = true (which makes sense if they're stubs),
                 *   and so when we enumerate the frames that might pertain to it, we just get managed frames back. It would appear to me that's actually
                 *   impossible to get an internal frame in V3, because CordbStackWalk::GetFrameWorker() asserts when the frame type is kExplicitFrame
                 */

                var process = accessor.Thread.Process;

                //If we can't query for frames because the thread has died, not much we can really do!
                switch (accessor.CorDebugThread.TryCreateStackWalk(out var stackWalk))
                {
                    case S_OK:
                        break;

                    case CORDBG_E_PROCESS_NOT_SYNCHRONIZED:
                        //If we did something naughty like set a breakpoint inside the CLR, we can't show a managed stack trace at this point,
                        //but we _can_ attempt to do a native stack trace instead!
                        if (process.Session.IsInterop)
                            return Native.Enumerate(accessor.Thread, nativeStackWalkerKind).ToArray();

                        return Array.Empty<CordbFrame>();

                    case CORDBG_E_CANT_CALL_ON_THIS_THREAD:
                        if (!process.IsV3 && process.Session.IsInterop)
                            return ResolveNativeFrames(process, accessor);

                        return Array.Empty<CordbFrame>();

                    default:
                        return Array.Empty<CordbFrame>();
                }

                var contextFlags = GetContextFlags(accessor.Thread.Process.MachineType);

                var results = new List<CordbFrame>();

                for (var hr = S_OK; hr != CORDBG_S_AT_END_OF_STACK; hr = stackWalk.TryNext())
                {
                    //Every frame needs to record what it's doing and where it's located in the stack
                    var context = new CrossPlatformContext(contextFlags, stackWalk.GetContext<CROSS_PLATFORM_CONTEXT>(contextFlags));

                    /* CordbStackWalk::GetFrame says the following about value it emits
                     * - When stopped at an "explicit frame", emits an CordbInternalFrame
                     * - When stopped at a "managed stack frame", emits an CordbNativeFrame
                     * - When stopped at a "native stack frame", emits null
                     *
                     * CordbStackWalk::GetFrameWorker() calls DacDbiInterfaceImpl::GetStackWalkCurrentFrameInfo(). This method returns a "FrameType" enum value.
                     * These FrameType values map to ICorDebug frame types as follows
                     * - kExplicitFrame:                     CordbInternalFrame - except not anymore!
                     * - kManagedStackFrame:                 CordbNativeFrame
                     * - kNativeRuntimeUnwindableStackFrame: CordbRuntimeUnwindableFrame
                     *
                     * CordbJITILFrame gets assigned to CordbNativeFrame->m_JITILFrame. When you QI the CordbNativeFrame, it delegates to the m_JITILFrame
                     * if it has a value. If it doesn't have a value, it's because either the frame has no metadata, or the frame's native code is "native only", which is described as
                     * "Is the function implemented natively in the runtime?? (eg, it has no IL, may be an Ecall/fcall)". A "native method" without an IL frame
                     * will have CorMethodImpl.miNative set in IMetaDataImport::GetMethodProps().pdwImplFlags (as per CordbFunction::InitNativeImpl()).
                     *
                     * In my investigations into useless CordbNativeFrame instances, the implementation kind was kUnknownImpl and fNoMetadata was true. */
                    if (stackWalk.TryGetFrame(out var frame).ThrowOnFailed() == S_FALSE || frame == null)
                    {
                        /* It is literally a native frame (kNativeStackFrame). We can't just do a native stack walk right now, because we have no
                         * way of truly knowing what's a native frame and what's not! But since our managed frames will report the same IP regardless
                         * of whether we query them via ICorDebug or a native stack walker, we'll simply make note that there's meant to be a
                         * transition here, and then at the end we can do a stack walk up until any managed frames that then follow. */
                        results.Add(new CordbNativeTransitionFrame(accessor.Thread, context));
                    }
                    else
                    {
                        //We have some known type of frame

                        if (frame is CorDebugRuntimeUnwindableFrame runtimeUnwindable)
                            throw new NotImplementedException($"Don't know how to handle a {frame.GetType().Name}");
                        else if (frame is CorDebugInternalFrame @internal)
                            throw new NotImplementedException($"V3 stack walker returned a {frame.GetType().Name}. This should be impossible.");
                        else if (frame is CorDebugNativeFrame native)
                            results.Add(GetRuntimeNativeFrame(process, accessor.Thread, native, context));
                        else if (frame is CorDebugILFrame il)
                            results.Add(GetILFrame(process, accessor.Thread, il, context));
                        else
                            throw new NotImplementedException($"Don't know how to handle a {frame.GetType().Name}");
                    }
                }

                if (!process.IsV3 && process.Session.IsInterop)
                    return ResolveAndMergeNativeFrames(process, nativeStackWalkerKind, accessor, results);

                return results.ToArray();
            }

            private static CordbFrame[] ResolveAndMergeNativeFrames(
                CordbProcess process,
                NativeStackWalkerKind nativeStackWalkerKind,
                CordbThread.ManagedAccessor accessor,
                List<CordbFrame> frames)
            {
                /* Ostensibly, we'd like to iterate over all of our frames, and any time we see a transition frame,
                 * we do a stack trace up until the next managed frame, and repeat to fill in all the gaps. Unfortunately,
                 * this doesn't work. Attempting to do a stack trace of a native frame in the middle of the stack either
                 * yields only a single frame, or spirals into wackiness. Thus, we'll employ a different strategy: take a complete
                 * native stack trace from the very top of the stack, and then iterate over all our existing managed frames, replacing
                 * all the transition frames we accumulated as appropriate. */

                //Shortcut out if we don't actually have any frames
                if (frames.Count == 0)
                    return Array.Empty<CordbFrame>();

                NativeFrame[] nativeFrames = nativeStackWalkerKind switch
                {
                    NativeStackWalkerKind.DbgHelp => Native.EnumerateDbgHelp(process, accessor.Handle, frames[0].Context),
                    NativeStackWalkerKind.DIA     => Native.EnumerateDIA(process, frames[0].Context),
                    _ => throw new UnknownEnumValueException(nativeStackWalkerKind)
                };

                //Concat our native and ICorDebug derived frames together. The stack pointer of each frame should increase
                //as you go from more recent function calls to older function calls. I'm not sure if this solution of merging
                //our native and managed frames together is either dodgy or genius
                var allFrames = nativeFrames
                    .Select(n => new {SP = n.FrameSP, Value = (object) n})
                    .Concat(frames.Select(f => new {f.Context.SP, Value = (object) f}))
                    .OrderBy(v => v.SP)
                    .ToArray();

                var newFrames = new List<CordbFrame>();

                for (var i = 0; i < allFrames.Length; i++)
                {
                    var item = allFrames[i];

                    if (item.Value is NativeFrame native)
                    {
                        //Is the frame before us a CordbFrame with the same SP? Ignore this NativeFrame!
                        if (i > 0 && allFrames[i - 1].Value is CordbFrame c1 && !(c1 is CordbNativeTransitionFrame) && c1.Context.SP == item.SP)
                            continue;

                        //Is the frame after us a CordbFrame with the same SP? Ignore this NativeFrame!
                        if (i < allFrames.Length - 1 && allFrames[i + 1].Value is CordbFrame c2 && !(c2 is CordbNativeTransitionFrame) && c2.Context.SP == item.SP)
                            continue;

                        process.Modules.TryGetModuleForAddress(native.FrameIP, out var module);

                        //It's a normal NativeFrame then
                        newFrames.Add(new CordbNativeFrame(native, accessor.Thread, module));
                    }
                    else
                    {
                        //All our transition frames are being replaced with "real" native frames
                        if (item.Value is CordbNativeTransitionFrame)
                            continue;

                        newFrames.Add((CordbFrame) item.Value);
                    }
                }

                return newFrames.ToArray();
            }

            private static CordbFrame[] ResolveNativeFrames(
                CordbProcess process,
                CordbThread.ManagedAccessor accessor)
            {
                using var walker = new NativeStackWalker(
                    process.Handle,
                    (IMemoryReader) process.DataTarget,
                    process.Symbols,
                    process.Session.PauseContext.DynamicFunctionTableCache,
                    (addr, inlineFrameContext) => GetModuleSymbol(addr, inlineFrameContext, process)
                );

                //First, let's get all native frames starting from the top of the stack
                var nativeFrames = walker.Walk(accessor.Handle, accessor.Thread.RegisterContext).ToArray();

                return nativeFrames.Select(v =>
                {
                    process.Modules.TryGetModuleForAddress(v.FrameIP, out var module);

                    return (CordbFrame) new CordbNativeFrame(v, accessor.Thread, module);
                }).ToArray();
            }

            private static CordbFrame GetRuntimeNativeFrame(CordbProcess process, CordbThread thread, CorDebugNativeFrame corDebugNativeFrame, CrossPlatformContext context)
            {
                /* It should be a "runtime native" frame. In the V3 stackwalker, this basically refers to frames that would have previously
                 * been referred to as "internal frames". A CorDebugNativeFrame does not contain an IL frame inside it when it either has no metadata,
                 * or when its function type is "native". In the latter case, there is metadata we can get at to name the function. */

                /* CordbFrame::GetFunction internally throws when you ask whether it has a function. This is bad for performance. We can circumvent this
                 * by instead calling CordbFrame::GetFunctionToken() internally (which may return null) and then checking whether the token is valid or not.
                 * Both a null function and an invalid function token would cause CordbFrame::GetFunction(ICorDebugFunction **ppFunction) to throw.
                 * Apparently, a nil methodDef can signify a dynamic function */
                if (corDebugNativeFrame.TryGetFunctionToken(out var methodDef) == S_OK && !methodDef.IsNil)
                {
                    //At this point there's no reason this should fail, but we'll still code defensively in case something changes in the future
                    if (corDebugNativeFrame.TryGetFunction(out var function) == S_OK)
                    {
                        //It sounds like it might be a P/Invoke or a QCall

                        var module = process.Modules.GetModule(function.Module);

                        var result = new CordbILTransitionFrame(corDebugNativeFrame, thread, module, context);

                        return result;
                    }
                }

                //It's a runtime native frame for sure then
                return new CordbRuntimeNativeFrame(corDebugNativeFrame, thread, null, context);
            }

            private static CordbFrame GetILFrame(CordbProcess process, CordbThread thread, CorDebugILFrame corDebugILFrame, CrossPlatformContext context)
            {
                var module = process.Modules.GetModule(corDebugILFrame.Function.Module);

                //Just a regular old IL frame
                return new CordbILFrame(corDebugILFrame, thread, module, context);
            }
        }
    }
}
