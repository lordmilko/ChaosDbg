namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a first chance exception that occurred in native code.
    /// </summary>
    public class CordbNativeFirstChanceExceptionPauseReason : CordbPauseReason
    {
        public CordbNativeFirstChanceExceptionPauseReason() : base(CordbEventHistoryType.NativeEvent)
        {
        }
    }
}
