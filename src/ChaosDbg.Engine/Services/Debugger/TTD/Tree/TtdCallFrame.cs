using System.Collections.Generic;

namespace ChaosDbg.TTD
{
    class TtdCallFrame : ITtdCallFrame
    {
        public TtdCallReturnEvent Event { get; }

        public TtdCallFrame(TtdCallReturnEvent @event)
        {
            Event = @event;
        }

        public ITtdCallFrame Parent { get; set; }
        public List<TtdCallFrame> Children { get; set; }
        public List<TtdIndirectJumpEvent> Indirects { get; set; }

        public override string ToString()
        {
            return Event.Name;
        }
    }
}
