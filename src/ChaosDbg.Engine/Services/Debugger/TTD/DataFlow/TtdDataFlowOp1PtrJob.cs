using System;
using System.Collections.Generic;
using System.Diagnostics;
using Iced.Intel;

namespace ChaosDbg.TTD
{
    internal class TtdDataFlowOp1PtrJob : TtdDataFlowJob //e.g. trace where rdi comes from in mov eax, [rdi]
    {
        public TtdDataFlowOp1PtrJob(TtdDataFlowItem parentEvent) : base(parentEvent)
        {
        }

        protected override IEnumerable<TtdDataFlowJob> EnumerateDataEventsInternal(TtdDataFlowContext ctx)
        {
            var instr = ParentEvent.Instruction.Instruction;

            Debug.Assert(instr.Op1Kind == OpKind.Memory);

            //Don't care about memory index; it's just an offset into the base pointer

            var registerContext = ctx.ParentToRegisterContext[ParentEvent];
            var registerValue = registerContext.GetRegisterValue(instr.MemoryBase);

            TtdDataFlowJob job;

            switch (instr.MemoryBase)
            {
                case Register.EIP:
                case Register.RIP:
                case Register.None:
                    throw new NotImplementedException($"Handling {instr.MemoryBase} is not implemented");

                default:
                    if (instr.MemoryIndex != Register.None)
                        yield break;

                    job = TraceRegisterValue(ctx, instr.MemoryBase, registerValue, 1);

                    if (job == null)
                        yield break;
                    break;
            }

            job.ParentEvent.Tag = TtdDataFlowTag.PointerOrigin;

            yield return job;
        }
    }
}
