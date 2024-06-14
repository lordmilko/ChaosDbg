using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a step in or over that was executed
    /// by the user within native code.
    /// </summary>
    public class CordbNativeStepEventPauseReason : CordbNativeEventPauseReason
    {
        public EXCEPTION_DEBUG_INFO Exception { get; }

        public CordbNativeStepEventPauseReason(bool outOfBand, EXCEPTION_DEBUG_INFO exception) : base(outOfBand)
        {
            Exception = exception;
        }
    }
}
