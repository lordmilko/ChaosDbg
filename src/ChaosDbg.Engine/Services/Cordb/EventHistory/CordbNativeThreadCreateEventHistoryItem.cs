using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbNativeThreadCreateEventHistoryItem : CordbNativeEventHistoryItem, ICordbThreadEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Create Thread {ThreadId}";

        public int ThreadId { get; }

        public CordbNativeThreadCreateEventHistoryItem(int threadId) : base(DebugEventType.CREATE_THREAD_DEBUG_EVENT)
        {
            ThreadId = threadId;
        }
    }
}
