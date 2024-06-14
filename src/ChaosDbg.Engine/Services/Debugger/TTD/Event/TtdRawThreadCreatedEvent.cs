using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public class TtdRawThreadCreatedEvent : TtdRawEvent
    {
        public ThreadId ThreadId { get; }

        public UniqueThreadId UniqueThreadId { get; }

        public unsafe TtdRawThreadCreatedEvent(ThreadCreatedEvent @event) : base(@event.position)
        {
            ThreadId = @event.info->ThreadId;
            UniqueThreadId = @event.info->UniqueThreadId;
        }
    }
}
