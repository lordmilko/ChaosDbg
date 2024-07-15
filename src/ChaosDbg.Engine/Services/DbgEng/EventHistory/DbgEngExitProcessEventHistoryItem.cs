using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngExitProcessEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        public DbgEngExitProcessEventHistoryItem(int processId) : base(DebugEventType.EXIT_PROCESS_DEBUG_EVENT, processId)
        {
        }
    }
}
