using System;
using ClrDebug.DbgEng;

namespace ChaosDbg
{
    public class EngineStatusChangedEventArgs : EventArgs
    {
        public DEBUG_STATUS OldStatus { get; }

        public DEBUG_STATUS NewStatus { get; }

        public EngineStatusChangedEventArgs(DEBUG_STATUS oldStatus, DEBUG_STATUS newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }

        public override string ToString()
        {
            return $"{OldStatus} -> {NewStatus}";
        }
    }
}
