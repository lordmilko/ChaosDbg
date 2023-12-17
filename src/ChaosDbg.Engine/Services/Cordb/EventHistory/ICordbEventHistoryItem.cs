namespace ChaosDbg.Cordb
{
    enum CordbEventHistoryType
    {
        ManagedEvent,
        NativeEvent,
        UserEvent
    }

    interface ICordbEventHistoryItem
    {
        public CordbEventHistoryType EventType { get; }

        public int EventThread { get; }
    }

    interface ICordbThreadEventHistoryItem : ICordbEventHistoryItem
    {
        int ThreadId { get; }
    }
}
