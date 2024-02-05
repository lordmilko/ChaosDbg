using System.Linq;
using ClrDebug;

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
            /// <param name="accessor">The accessor that is used to interact with the native thread.</param>
            /// <returns>A list of all frames in the stack trace.</returns>
            public static CordbFrame[] Enumerate(CordbThread.NativeAccessor accessor)
            {
                var process = accessor.Thread.Process;
                var dataTarget = (ICLRDataTarget) process.DAC.DataTarget;

                using var walker = new NativeStackWalker(
                    dataTarget,
                    process.DbgHelp,
                    process.Session.PauseContext.DynamicFunctionTableCache,
                    addr => GetModuleSymbol(addr, process)
                );

                var contextFlags = GetContextFlags(process.MachineType);
                var context = new CrossPlatformContext(contextFlags, dataTarget.GetThreadContext<CROSS_PLATFORM_CONTEXT>(accessor.Id, contextFlags));

                var frames = walker.Walk(process.Handle, accessor.Handle, context).ToArray();

                return frames.Select(f => (CordbFrame) new CordbNativeFrame(f, process.Modules.GetModuleForAddress(f.IP))).ToArray();
            }
        }
    }
}
