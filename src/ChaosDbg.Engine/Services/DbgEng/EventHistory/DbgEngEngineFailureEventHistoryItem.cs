using System;

namespace ChaosDbg.DbgEng
{
    class DbgEngEngineFailureEventHistoryItem : DbgEngEventHistoryItem
    {
        public Exception Exception { get; }

        public EngineFailureStatus Status { get; }

        public DbgEngEngineFailureEventHistoryItem(Exception exception, EngineFailureStatus status) : base(DbgEngEventHistoryType.Engine)
        {
            Exception = exception;
            Status = status;
        }

        public override string ToString()
        {
            return Status.ToString();
        }
    }
}
