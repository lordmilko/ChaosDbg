using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbManagedThreadCreateEventHistoryItem : CordbManagedEventHistoryItem, ICordbThreadEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Create Thread {VolatileOSThreadID}";

        public int ThreadId { get; }

        public int VolatileOSThreadID { get; }

        public CordbManagedThreadCreateEventHistoryItem(CreateThreadCorDebugManagedCallbackEventArgs e) : base(e.Kind)
        {
            ThreadId = e.Thread.Id;
            VolatileOSThreadID = e.Thread.VolatileOSThreadID;
        }
    }
}
