using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public class TtdRawModuleUnloadedEvent : TtdRawEvent
    {
        public GuestAddress Address { get; }

        public string Name { get; }

        public int Size { get; }

        public short Timestamp { get; }

        public unsafe TtdRawModuleUnloadedEvent(ModuleUnloadedEvent @event) : base(@event.position)
        {
            Address = @event.info->address;
            Name = @event.info->ToString();
            Size = (int) @event.info->size;
            Timestamp = @event.info->timestamp;
        }
    }
}
