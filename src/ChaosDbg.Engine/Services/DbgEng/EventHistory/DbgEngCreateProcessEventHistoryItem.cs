using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngCreateProcessEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Create Process {Name}";

        public string Name { get; }

        public DbgEngCreateProcessEventHistoryItem(int processId, string name) : base(DebugEventType.CREATE_PROCESS_DEBUG_EVENT, processId)
        {
            Name = name;
        }
    }
}
