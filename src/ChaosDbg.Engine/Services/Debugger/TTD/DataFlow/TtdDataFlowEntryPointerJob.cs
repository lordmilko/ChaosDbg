using System.Collections.Generic;

namespace ChaosDbg.TTD
{
    //We begin our trace by tracing the first modification event against a given buffer pointer
    internal class TtdDataFlowEntryPointerJob : TtdDataFlowJob
    {
        private long address;

        public TtdDataFlowEntryPointerJob(long address, TtdDataFlowItem parentEvent) : base(parentEvent)
        {
            this.address = address;
        }

        protected override IEnumerable<TtdDataFlowJob> EnumerateDataEventsInternal(TtdDataFlowContext ctx)
        {
            var item = TraceBufferValue(address, ctx, ctx.ValueSize);

            return new[] {new TtdDataFlowJob(item)};
        }
    }
}
