namespace ChaosDbg.Cordb
{
    public enum CordbEventHistoryType
    {
        ManagedEvent,
        NativeEvent,
        UserEvent,
        Engine
    }

    interface ICordbEventHistoryItem
    {
        public CordbEventHistoryType EventType { get; }

        /// <summary>
        /// Gets the operating system ID of the thread the event occurred on.
        /// </summary>
        public int EventThread { get; }
    }

    interface ICordbThreadEventHistoryItem : ICordbEventHistoryItem
    {
        int ThreadId { get; }
    }
}
