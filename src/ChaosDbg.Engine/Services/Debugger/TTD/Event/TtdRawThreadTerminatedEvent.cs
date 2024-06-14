using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public class TtdRawThreadTerminatedEvent : TtdRawEvent
    {
        public ThreadId ThreadId { get; }

        public UniqueThreadId UniqueThreadId { get; }

        public unsafe TtdRawThreadTerminatedEvent(ThreadTerminatedEvent @event) : base(@event.position)
        {
            ThreadId = @event.info->ThreadId;
            UniqueThreadId = @event.info->UniqueThreadId;
        }
    }
}
