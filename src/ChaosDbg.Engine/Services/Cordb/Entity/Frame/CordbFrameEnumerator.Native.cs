using System;
using System.Collections.Generic;
using System.Linq;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug;
using ClrDebug.DIA;

namespace ChaosDbg.Cordb
{
    internal partial class CordbFrameEnumerator
    {
        /// <summary>
        /// Implements a stack frame enumerator compatible with purely native threads inside a managed process.
        /// </summary>
        public static class Native
        {
            /// <summary>
            /// Enumerates all frames in a thread that has so far never attempted to execute managed code.
            /// </summary>
            /// <param name="thread">The thread to retrieve a stack trace of. This will typically be a thread with a native accessor,
            /// however in the event we're doing interop debugging and can't show a managed stack trace because we set a breakpoint
            /// inside the CLR, we'll fallback to doing a native stack trace instead.</param>
            /// <param name="nativeStackWalkerKind">Specifies the native stack walking engine that should be used for unwinding frames.</param>
            /// <returns>A list of all frames in the stack trace.</returns>
            public static IEnumerable<CordbFrame> Enumerate(CordbThread thread, NativeStackWalkerKind nativeStackWalkerKind)
            {
                var process = thread.Process;

                var nativeFrames = nativeStackWalkerKind switch
                {
                    NativeStackWalkerKind.DbgHelp => EnumerateDbgHelp(process, thread.Handle, thread.RegisterContext),
                    NativeStackWalkerKind.DIA => EnumerateDIA(process, thread.RegisterContext),
                    _ => throw new UnknownEnumValueException(nativeStackWalkerKind)
                };

                foreach (var rawFrame in nativeFrames)
                {
                    process.Modules.TryGetModuleForAddress(rawFrame.FrameIP, out var module);

                    var frame = (CordbFrame) new CordbNativeFrame(rawFrame, thread, module);

                    yield return frame;
                }
            }

            internal static NativeFrame[] EnumerateDbgHelp(CordbProcess process, IntPtr hThread, CrossPlatformContext context)
            {
                using var walker = new NativeStackWalker(
                    process.Handle,
                    (IMemoryReader) process.DataTarget,
                    process.Symbols,
                    process.Session.PauseContext.DynamicFunctionTableCache,
                    (addr, inlineFrameContext) => GetModuleSymbol(addr, inlineFrameContext, process)
                );

                var nativeFrames = walker.Walk(hThread, context).ToArray();

                return nativeFrames;
            }

            internal static NativeFrame[] EnumerateDIA(CordbProcess process, CrossPlatformContext context)
            {
                var symbolProvider = process.Symbols;

                static unsafe IDiaStackWalkHelper CreateDiaStackWalkHelper(
                    IMemoryReader memoryReader,
                    IntPtr pCrossPlatformContext,
                    DiaTryGetModuleDelegate getModule,
                    DiaTryGetFunctionTableEntryDelegate getFunctionTable)
                {
                    return new DiaStackWalkHelper(memoryReader, (CROSS_PLATFORM_CONTEXT*) pCrossPlatformContext, getModule, getFunctionTable);
                }

                var diaFrames = symbolProvider.DiaStackWalk(
                    process.Handle,
                    context.Raw,
                    context.Flags,
                    process.Session.PauseContext.DynamicFunctionTableCache,
                    CreateDiaStackWalkHelper
                );

                return diaFrames;
            }
        }
    }
}
