namespace ChaosDbg.Cordb
{
    public class CordbUserBreakPauseReason : CordbPauseReason
    {
        public CordbUserBreakPauseReason() : base(CordbEventHistoryType.UserEvent)
        {
        }
    }
}
