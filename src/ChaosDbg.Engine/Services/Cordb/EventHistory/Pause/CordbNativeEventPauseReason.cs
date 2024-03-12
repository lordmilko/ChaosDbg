namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a native debug event.
    /// </summary>
    public abstract class CordbNativeEventPauseReason : CordbPauseReason
    {
        /// <summary>
        /// Gets whether this event was an out of band event.
        /// </summary>
        public bool OutOfBand { get; }

        protected CordbNativeEventPauseReason(bool outOfBand) : base(CordbEventHistoryType.NativeEvent)
        {
            OutOfBand = outOfBand;
        }
    }
}
