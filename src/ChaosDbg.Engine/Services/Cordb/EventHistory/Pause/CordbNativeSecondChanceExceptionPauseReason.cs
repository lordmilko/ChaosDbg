using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a second chance (unhandled) exception that occurred in native code.
    /// </summary>
    public class CordbNativeSecondChanceExceptionPauseReason : CordbNativeEventPauseReason
    {
        public EXCEPTION_RECORD ExceptionRecord { get; }

        public CordbNativeSecondChanceExceptionPauseReason(EXCEPTION_RECORD exceptionRecord, bool outOfBand) : base(outOfBand)
        {
            ExceptionRecord = exceptionRecord;
        }
    }
}
