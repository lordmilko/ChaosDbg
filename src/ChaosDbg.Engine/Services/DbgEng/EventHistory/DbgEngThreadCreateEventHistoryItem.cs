using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngThreadCreateEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Create Thread {ThreadId}";

        public int ThreadId { get; }

        public DbgEngThreadCreateEventHistoryItem(int processId, int threadId) : base(DebugEventType.CREATE_THREAD_DEBUG_EVENT, processId)
        {
            ThreadId = threadId;
        }
    }
}
