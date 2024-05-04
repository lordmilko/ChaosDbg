using System;
using ChaosLib;

namespace ChaosDbg.Cordb
{
    class CordbEngineFailureEventHistoryItem : ICordbEventHistoryItem
    {
        #region ICordbEventHistoryItem

        public CordbEventHistoryType EventType { get; }

        public int EventThread { get; }

        #endregion

        public Exception Exception { get; }

        public EngineFailureStatus Status { get; }

        public CordbEngineFailureEventHistoryItem(Exception exception, EngineFailureStatus status)
        {
            Exception = exception;
            Status = status;
            EventType = CordbEventHistoryType.Engine;
            EventThread = Kernel32.GetCurrentThreadId();
        }

        public override string ToString()
        {
            return Status.ToString();
        }
    }
}
