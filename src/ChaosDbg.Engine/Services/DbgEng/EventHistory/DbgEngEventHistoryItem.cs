using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.DbgEng
{
    public enum DbgEngEventHistoryType
    {
        NativeEvent,
        UserEvent,
        Engine
    }

    public abstract class DbgEngEventHistoryItem
    {
        DbgEngEventHistoryType EventType { get; }

        /// <summary>
        /// Gets the operating system ID of the thread the event occurred on.
        /// </summary>
        int EventThread { get; }

        protected DbgEngEventHistoryItem(DbgEngEventHistoryType eventType)
        {
            EventType = eventType;
            EventThread = Kernel32.GetCurrentThreadId();
        }
    }

    [DebuggerDisplay("[{EventType}] {ProcessId} : {DebuggerDisplay,nq}")]
    public abstract class DbgEngNativeEventHistoryItem : DbgEngEventHistoryItem
    {
        protected virtual string DebuggerDisplay => NativeEventKind.ToString();

        public int ProcessId { get; }

        public DebugEventType NativeEventKind { get; }

        protected DbgEngNativeEventHistoryItem(DebugEventType nativeEventKind, int processId) : base(DbgEngEventHistoryType.NativeEvent)
        {
            NativeEventKind = nativeEventKind;
            ProcessId = processId;
        }
    }
}
