using System.Collections.Generic;

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
            /// <returns>A list of all frames in the stack trace.</returns>
            public static IEnumerable<CordbFrame> Enumerate(CordbThread thread)
            {
                var process = thread.Process;
                var dataTarget = process.DataTarget;

                using var walker = new NativeStackWalker(
                    dataTarget,
                    process.Symbols,
                    process.Session.PauseContext.DynamicFunctionTableCache,
                    (addr, inlineFrameContext) => GetModuleSymbol(addr, inlineFrameContext, process)
                );

                var rawFrames = walker.Walk(process.Handle, thread.Handle, thread.RegisterContext);

                foreach (var rawFrame in rawFrames)
                {
                    process.Modules.TryGetModuleForAddress(rawFrame.FrameIP, out var module);

                    var frame = (CordbFrame) new CordbNativeFrame(rawFrame, thread, module);

                    yield return frame;
                }
            }
        }
    }
}
