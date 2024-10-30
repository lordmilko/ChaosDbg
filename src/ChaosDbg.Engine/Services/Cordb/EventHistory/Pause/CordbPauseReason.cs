namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides information about why the normal debugger loop was stopped to prompt for user input.
    /// </summary>
    public abstract class CordbPauseReason : CordbEventHistoryItem
    {
        protected CordbPauseReason(CordbEventHistoryType eventType) : base(eventType)
        {
        }
    }
}
