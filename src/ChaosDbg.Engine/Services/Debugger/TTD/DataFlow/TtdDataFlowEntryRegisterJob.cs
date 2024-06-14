using System.Collections.Generic;
using Iced.Intel;

namespace ChaosDbg.TTD
{
    internal class TtdDataFlowEntryRegisterJob : TtdDataFlowJob
    {
        private Register register;

        public TtdDataFlowEntryRegisterJob(Register register, TtdDataFlowItem parentEvent) : base(parentEvent)
        {
            this.register = register;
        }

        protected override IEnumerable<TtdDataFlowJob> EnumerateDataEventsInternal(TtdDataFlowContext ctx)
        {
            yield return TraceRegisterValue(ctx, register, ctx.TargetValue, 1);
        }
    }
}
