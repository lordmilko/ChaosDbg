namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a step in or over that was executed
    /// by the user within native code.
    /// </summary>
    public class CordbNativeStepEventPauseReason : CordbNativeEventPauseReason
    {
        public CordbNativeStepEventPauseReason(bool outOfBand) : base(outOfBand)
        {
        }
    }
}
