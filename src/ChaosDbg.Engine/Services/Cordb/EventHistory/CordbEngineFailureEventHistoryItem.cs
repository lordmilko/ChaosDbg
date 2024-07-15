using System;
using ChaosLib;

namespace ChaosDbg.Cordb
{
    class CordbEngineFailureEventHistoryItem : CordbEventHistoryItem
    {
        public Exception Exception { get; }

        public EngineFailureStatus Status { get; }

        public CordbEngineFailureEventHistoryItem(Exception exception, EngineFailureStatus status) : base(CordbEventHistoryType.Engine)
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
