using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbManagedThreadExitEventHistoryItem : CordbManagedEventHistoryItem, ICordbThreadEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Exit Thread {VolatileOSThreadID}";

        public int ThreadId { get; }

        public int VolatileOSThreadID { get; }

        public CordbManagedThreadExitEventHistoryItem(ExitThreadCorDebugManagedCallbackEventArgs e) : base(e.Kind)
        {
            ThreadId = e.Thread.Id;
            VolatileOSThreadID = e.Thread.VolatileOSThreadID;
        }
    }
}
