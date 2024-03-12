namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a native breakpoint
    /// that is either known to the debugger, or is a special breakpoint kind (such as the loader breakpoint).
    /// </summary>
    public class CordbNativeBreakpointEventPauseReason : CordbNativeEventPauseReason
    {
        public CordbNativeBreakpointEventPauseReason(bool outOfBand) : base(outOfBand)
        {
        }
    }
}
