using System.Collections.Generic;
using System.Threading;
using Iced.Intel;

namespace ChaosDbg.TTD
{
    internal class TtdDataFlowEntryRegisterJob : TtdDataFlowJob
    {
        private Register register;
        private CrossPlatformContext registerContext;

        public TtdDataFlowEntryRegisterJob(Register register, CrossPlatformContext registerContext, TtdDataFlowItem parentEvent) : base(parentEvent)
        {
            this.register = register;
            this.registerContext = registerContext;
        }

        protected override IEnumerable<TtdDataFlowJob> EnumerateDataEventsInternal(TtdDataFlowContext ctx, CancellationToken cancellationToken)
        {
            yield return TraceRegisterValue(
                ctx: ctx,
                register: register,
                currentRegisterContext: registerContext,
                futureRegisterValue: ctx.TargetValue,
                registerIndex: 1,
                cancellationToken: cancellationToken
            );
        }
    }
}
