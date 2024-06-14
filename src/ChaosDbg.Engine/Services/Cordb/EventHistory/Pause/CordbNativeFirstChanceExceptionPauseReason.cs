using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a first chance exception that occurred in native code.
    /// </summary>
    public class CordbNativeFirstChanceExceptionPauseReason : CordbNativeEventPauseReason
    {
        public EXCEPTION_RECORD ExceptionRecord { get; }

        public CordbNativeFirstChanceExceptionPauseReason(EXCEPTION_RECORD exceptionRecord, bool outOfBand) : base(outOfBand)
        {
            ExceptionRecord = exceptionRecord;
        }
    }
}
