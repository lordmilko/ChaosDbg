using ChaosLib;

namespace ChaosDbg.Cordb
{
    public enum CordbEventHistoryType
    {
        ManagedEvent,
        NativeEvent,
        UserEvent,
        Engine
    }

    public abstract class CordbEventHistoryItem
    {
        CordbEventHistoryType EventType { get; }

        /// <summary>
        /// Gets the operating system ID of the thread the event occurred on.
        /// </summary>
        int EventThread { get; }

        protected CordbEventHistoryItem(CordbEventHistoryType eventType)
        {
            EventType = eventType;
            EventThread = Kernel32.GetCurrentThreadId();
        }
    }

    interface ICordbThreadEventHistoryItem
    {
        int ThreadId { get; }
    }
}
