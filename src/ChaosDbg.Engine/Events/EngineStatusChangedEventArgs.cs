using System;

namespace ChaosDbg
{
    public class EngineStatusChangedEventArgs : EventArgs
    {
        public EngineStatus OldStatus { get; }

        public EngineStatus NewStatus { get; }

        public EngineStatusChangedEventArgs(EngineStatus oldStatus, EngineStatus newStatus)
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
