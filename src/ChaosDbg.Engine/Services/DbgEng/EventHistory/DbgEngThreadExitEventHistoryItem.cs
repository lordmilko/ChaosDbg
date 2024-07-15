using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngThreadExitEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Exit Thread {ThreadId}";

        public int ThreadId { get; }

        public DbgEngThreadExitEventHistoryItem(int processId, int threadId) : base(DebugEventType.EXIT_THREAD_DEBUG_EVENT, processId)
        {
            ThreadId = threadId;
        }
    }
}
