using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    [DebuggerDisplay("[{EventType}] {DebuggerDisplay,nq}")]
    class CordbNativeEventHistoryItem : ICordbEventHistoryItem
    {
        protected virtual string DebuggerDisplay => NativeEventKind.ToString();

        public CordbEventHistoryType EventType => CordbEventHistoryType.NativeEvent;

        public int EventThread { get; }

        public DebugEventType NativeEventKind { get; }

        public static CordbNativeEventHistoryItem New(DebugEventCorDebugUnmanagedCallbackEventArgs e)
        {
            switch (e.DebugEvent.dwDebugEventCode)
            {
                case DebugEventType.CREATE_THREAD_DEBUG_EVENT:
                    return new CordbNativeThreadCreateEventHistoryItem(e.DebugEvent.dwThreadId);

                case DebugEventType.EXIT_THREAD_DEBUG_EVENT:
                    return new CordbNativeThreadExitEventHistoryItem(e.DebugEvent.dwThreadId);

                default:
                    return new CordbNativeEventHistoryItem(e.DebugEvent.dwDebugEventCode);
            }
        }

        public static CordbNativeEventHistoryItem CreateProcess() =>
            new CordbNativeEventHistoryItem(DebugEventType.CREATE_PROCESS_DEBUG_EVENT);

        public static CordbNativeEventHistoryItem CreateThread(int threadId) =>
            new CordbNativeThreadCreateEventHistoryItem(threadId);

        protected CordbNativeEventHistoryItem(DebugEventType debugEventType)
        {
            NativeEventKind = debugEventType;
            EventThread = Kernel32.GetCurrentThreadId();
        }

        public override string ToString() => DebuggerDisplay;
    }
}
