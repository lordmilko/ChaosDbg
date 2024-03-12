namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a second chance (unhandled) exception that occurred in native code.
    /// </summary>
    public class CordbNativeSecondChanceExceptionPauseReason : CordbPauseReason
    {
        public CordbNativeSecondChanceExceptionPauseReason() : base(CordbEventHistoryType.NativeEvent)
        {
        }
    }
}
