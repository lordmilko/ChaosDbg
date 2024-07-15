namespace ChaosDbg.DbgEng
{
    public abstract class DbgEngPauseReason : DbgEngEventHistoryItem
    {
        protected DbgEngPauseReason(DbgEngEventHistoryType eventType) : base(eventType)
        {
        }
    }
}
