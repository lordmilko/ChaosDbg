using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Specifies types of reasons that can cause the normal debugger loop to be stopped to prompt for user input.
    /// </summary>
    public enum CordbPauseReasonKind
    {
        None = 0,

        /// <summary>
        /// Specifies that the user called <see cref="CordbEngine.Break"/> to force a break into the debugger.
        /// </summary>
        UserBreak,

        /// <summary>
        /// Specifies that a <see cref="CordbNativeCodeBreakpoint"/>, <see cref="CordbRawCodeBreakpoint"/> or <see cref="CordbDataBreakpoint"/> was hit.
        /// </summary>
        NativeBreakpointEvent,

        /// <summary>
        /// Specifies that a <see cref="CordbStepBreakpoint"/> was hit.
        /// </summary>
        NativeStepEvent,

        /// <summary>
        /// Specifies that a <see cref="DebugEventType.EXCEPTION_DEBUG_EVENT"/> was received that was not handled by the debugger and was passed back to user code.
        /// </summary>
        NativeFirstChanceException,

        /// <summary>
        /// Specifies that a <see cref="DebugEventType.EXCEPTION_DEBUG_EVENT"/> was received for an exception that neither the debugger nor the user process was able to handle,
        /// indicating that target process is about to crash.
        /// </summary>
        NativeSecondChanceException,
    }
}