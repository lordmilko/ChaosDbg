namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbPauseReason"/> that was caused as a result of a managed debug event.
    /// </summary>
    public abstract class CordbManagedEventPauseReason : CordbPauseReason
    {
        protected CordbManagedEventPauseReason() : base(CordbEventHistoryType.ManagedEvent)
        {
        }
    }
}
