using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbNativeThreadExitEventHistoryItem : CordbNativeEventHistoryItem, ICordbThreadEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Exit Thread {ThreadId}";

        public int ThreadId { get; }

        public CordbNativeThreadExitEventHistoryItem(int threadId) : base(DebugEventType.EXIT_THREAD_DEBUG_EVENT)
        {
            ThreadId = threadId;
        }
    }
}
