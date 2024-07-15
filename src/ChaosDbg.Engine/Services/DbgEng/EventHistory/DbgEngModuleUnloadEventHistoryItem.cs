using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngModuleUnloadEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Unload {Name}";

        public string Name { get; }

        public long BaseAddress { get; }

        public DbgEngModuleUnloadEventHistoryItem(string name, long baseAddress, int processId) : base(DebugEventType.UNLOAD_DLL_DEBUG_EVENT, processId)
        {
            Name = name;
            BaseAddress = baseAddress;
        }
    }
}
