using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ChaosDbg.Disasm;
using ChaosLib;
using ClrDebug.TTD;
using Iced.Intel;

namespace ChaosDbg.TTD
{
    [DebuggerDisplay("{ParentEvent.Instruction?.ToString()}")]
    internal class TtdDataFlowJob
    {
        public TtdDataFlowItem ParentEvent { get; }

        public TtdDataFlowJob(TtdDataFlowItem parentEvent)
        {
            ParentEvent = parentEvent;
        }

        internal IEnumerable<TtdDataFlowJob> EnumerateDataEvents(TtdDataFlowContext ctx, CancellationToken cancellationToken)
        {
            if (ctx.Cursor.GetPosition(ParentEvent.Thread.ThreadId) != ParentEvent.Position)
                ctx.Cursor.SetPosition(ParentEvent.Position);

            foreach (var item in EnumerateDataEventsInternal(ctx, cancellationToken))
            {
                //If this item was resolved in the context of tracing the pointer that contained the actual value we're after,
                //this child event now needs to be tagged as such as well
                if (ParentEvent.Tag != TtdDataFlowTag.ValueOrigin)
                    item.ParentEvent.Tag = ParentEvent.Tag;

                yield return item;
            }
        }

        protected virtual IEnumerable<TtdDataFlowJob> EnumerateDataEventsInternal(TtdDataFlowContext ctx, CancellationToken cancellationToken)
        {
            var instr = ParentEvent.Instruction.Instruction;

            switch (instr.Mnemonic)
            {
                case Mnemonic.Mov:
                case Mnemonic.Movups:
                case Mnemonic.Lea:
                    foreach (var item in ProcessOp2(ctx, cancellationToken))
                        yield return item;
                    break;

                default:
                    //e.g. if it's an Add, the value was just synthesized out of nowhere and it's over!
                    yield break;
            }
        }

        private IEnumerable<TtdDataFlowJob> ProcessOp2(TtdDataFlowContext ctx, CancellationToken cancellationToken)
        {
            var instr = ParentEvent.Instruction.Instruction;

            if (instr.OpCount != 2)
                throw new NotImplementedException($"Don't know how to handle instruction {instr} with {instr.OpCount} operands");

            var registerContext = ctx.ParentToRegisterContext[ParentEvent];

            //Whatever Op0 is is irrelevant; i.e. whether Op0 is a register or another memory location doesn't matter.
            //The fact we're in this method means that a value was placed in Op0 that we care about; our responsibility is to now trace where the value in Op1 came from

            switch (instr.Op1Kind)
            {
                case OpKind.Register:
                    //The value in Op0 (which is either a register or a memory location) got its value from another register
                    var result = TraceRegisterValue(
                        ctx: ctx,
                        register: instr.Op1Register,
                        currentRegisterContext: registerContext,
                        futureRegisterValue: registerContext.GetRegisterValue<Int128>(instr.Op1Register),
                        registerIndex: 1,
                        cancellationToken: cancellationToken
                    );
                    ParentEvent.Location = instr.Op1Register;

                    if (result != null)
                        yield return result;

                    yield break;

                case OpKind.Memory:
                    //The value in Op0 (which is either a register or a memory location) got its value from a pointer to a memory location
                    var size = ctx.ValueSize;

                    if (!TryGetPointer(ctx.Cursor, instr, registerContext, ParentEvent.Tag, ctx.TargetValue, ref size, out var memoryAddress))
                        yield break;

                    var dataFlowItem = TraceBufferValue(memoryAddress, ctx, size);
                    ParentEvent.Location = (GuestAddress) memoryAddress;
                    yield return new TtdDataFlowJob(dataFlowItem);
                    yield return new TtdDataFlowOp1PtrJob(ParentEvent);
                    yield break;

                default:
                    throw new UnknownEnumValueException(instr.Op1Kind);
            }
        }

        private static bool TryGetPointer(
            Cursor cursor,
            in Instruction instr,
            CrossPlatformContext registerContext,
            TtdDataFlowTag tag,
            long targetValue,
            ref int size,
            out long pointerValue)
        {
            long registerValue;

            switch (instr.MemoryBase)
            {
                case Register.RIP:
                    //It's just an absolute value
                    registerValue = registerContext.GetRegisterValue(instr.MemoryBase);

                    //todo: read it and assert whats there?

                    pointerValue = registerValue;
                    return true;

                case Register.EIP:
                case Register.None:
                    throw new NotImplementedException($"Handling {instr.MemoryBase} is not implemented");

                default:
                    long ptrOffset = 0;

                    if (instr.MemoryDisplacement64 != 0)
                        ptrOffset = (long) instr.MemoryDisplacement64;

                    if (instr.MemoryIndex != Register.None)
                    {
                        var indexRegisterValue = registerContext.GetRegisterValue(instr.MemoryIndex);

                        if (instr.MemoryIndexScale != 1)
                            indexRegisterValue *= instr.MemoryIndexScale;

                        ptrOffset += indexRegisterValue;
                    }
                    else
                    {
                        //Don't know how to handle having a memory index scale without a memory index
                        if (instr.MemoryIndexScale != 1)
                            throw new NotImplementedException("Handling a non-1 memory scale is not implemented");
                    }

                    //Currently we only support [register] with no modifiers
                    registerValue = registerContext.GetRegisterValue(instr.MemoryBase);

                    //We need to read a variable amount of memory based on the size of the destination

                    if (instr.Op0Kind != OpKind.Register)
                        throw new UnknownEnumValueException(instr.Op0Kind);

                    registerValue += ptrOffset;

                    Int128 ptrValue;

                    var opSize = instr.Op0Register.GetInfo().Size;

                    switch (opSize)
                    {
                        case 8:
                            if (registerValue == targetValue)
                            {
                                //There's no pointer to read; this likely indicates that the value we're chasing is something like lea rdx,rsp+20. rsp+20 _is_
                                //the value we're after, it doesn't "come" from anywhere else
                                pointerValue = default;
                                return false;
                            }

                            //registerValue and targetValue are equal when we're tracing the value in a register, rather than what it points to
                            ptrValue = cursor.QueryMemoryBuffer<long>(registerValue, QueryMemoryPolicy.Default);

                            if (tag == TtdDataFlowTag.PointerOrigin && ptrValue != targetValue)
                                ptrValue = cursor.QueryMemoryBuffer<long>((long) ptrValue.Lower, QueryMemoryPolicy.Default);

                            Debug.Assert(ptrValue == targetValue);

                            break;

                        case 16:
                            //If we're doing a buffer move from an xmm register, the value we're after may be in the upper half instead of the lower half.
                            //In that circumstance, upgrade the size we want to lookout for to 16 bytes
                            size = 16;
                            ptrValue = cursor.QueryMemoryBuffer<Int128>(registerValue, QueryMemoryPolicy.Default);
                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle instruction {instr} with an operand size of {opSize}");
                    }

                    pointerValue = registerValue;
                    return true;
            }
        }

        internal unsafe TtdDataFlowJob TraceRegisterValue(
            TtdDataFlowContext ctx,
            Register register,
            CrossPlatformContext currentRegisterContext,
            Int128 futureRegisterValue,
            int registerIndex,
            CancellationToken cancellationToken)
        {
            if (registerIndex != 1)
                throw new NotImplementedException("Don't know how to handle a register index that is not 1");

            /* The Cursor.SetRegisterChangedCallback does not seem to do what I want. It seems to just blast events at you, can't be stopped,
             * doesn't even do anything if the step count is 1, and doesn't seem to fire if we've already removed our previous memory
             * watchpoint. So, Plan B: manually step back until the point that our target register loses its value */

            if (ParentEvent.Tag == TtdDataFlowTag.PointerOrigin || this is TtdDataFlowOp1PtrJob)
            {
                if (futureRegisterValue.Upper != (ulong) ctx.TargetValue && futureRegisterValue.Lower != (ulong) ctx.TargetValue)
                {
                    Debug.Assert(futureRegisterValue.Upper == 0);
                    var pointedValue = ctx.Cursor.QueryMemoryBuffer<IntPtr>((long) futureRegisterValue.Lower, QueryMemoryPolicy.Default);

                    //If the value came from an offset (e.g. [rbx+70h]) we're at the end of the road for this particular branchway of where the value came from

                    if ((long) (void*) pointedValue != ctx.TargetValue)
                        return null;
                }
            }
            else
            {
                Debug.Assert(futureRegisterValue.Upper == (ulong) ctx.TargetValue || futureRegisterValue.Lower == (ulong) ctx.TargetValue);
            }

            while (true)
            {
                var replayResult = ctx.Cursor.ReplayBackward(Position.Min, 1);
        private bool ReverseStepOverInstr(
            TtdDataFlowContext ctx,
            Register register,
            ref CrossPlatformContext previousContext,
            Int128 futureRegisterValue,
            out INativeInstruction currentInstr,
            CancellationToken cancellationToken)
        {
            var replayResult = ctx.Cursor.ReplayBackward(Position.Min, 1);

            if (replayResult.EventType != EventType.StepCount)
                throw new UnknownEnumValueException(replayResult.EventType);

            var stepInPosition = ctx.Cursor.GetPosition();

            cancellationToken.ThrowIfCancellationRequested();

            var stepRegisterContext = ctx.Cursor.GetCrossPlatformContext(ParentEvent.Thread.ThreadId);

            ctx.Disassembler.BaseStream.Position = stepRegisterContext.IP;
            var instr = ctx.Disassembler.Disassemble();

                //Has the value in our target register changed yet?
                var pastRegisterValue = registerContext.GetRegisterValue<Int128>(register);

            if (stepRegisterContext.SP != previousContext.SP && instr.Instruction.Mnemonic == Mnemonic.Ret)
            {
                var watchpoint = new MemoryWatchpointData
                {
                    address = previousContext.IP - 1,
                    size = 1,
                    flags = BP_FLAGS.EXEC
                };

                //We just reverse stepped into a function call! Set a breakpoint on the instruction just before the instruction we were on before we rewound
                ctx.Cursor.AddMemoryWatchpoint(watchpoint);

                replayResult = ctx.Cursor.ReplayBackward(Position.Min, StepCount.Max);

                ctx.Cursor.RemoveMemoryWatchpoint(watchpoint);

                if (replayResult.EventType != 0)
                    throw new UnknownEnumValueException(replayResult.EventType);

                var watchpointContext = ctx.Cursor.GetCrossPlatformContext(ParentEvent.Thread.ThreadId);

                var pastRegisterValue = watchpointContext.GetRegisterValue<Int128>(register);

                if (pastRegisterValue != futureRegisterValue)
                {
                    //The value changed inside the function we just stepped over. Go back to where we were when we stepped over the function originally so we can
                    //step through the function we reverse stepped into instruction by instruction
                    ctx.Cursor.SetPosition(stepInPosition);

                    previousContext = stepRegisterContext;
                }
                else
                {
                    //We don't need to step through the function we skipped over. The current context is the context from after we stepped back out of the function
                    previousContext = watchpointContext;

                    //We're now on top of the call instruction. The next time the loop runs, we'll move to the instruction before it
                }
            }
            else
            {
                var pastRegisterValue = stepRegisterContext.GetRegisterValue<Int128>(register);

                previousContext = stepRegisterContext;

                if (pastRegisterValue != futureRegisterValue)
                {
                    //We found the point where the register value changed!
                    currentInstr = instr;
                    return true;
                }
            }

            currentInstr = default;
            return false;
        }

        protected unsafe TtdDataFlowItem TraceBufferValue(long address, TtdDataFlowContext ctx, int size)
        {
            var watchpoint = new MemoryWatchpointData
            {
                address = address,
                flags = BP_FLAGS.WRITE,
                size = size
            };

            if (ParentEvent.Tag != TtdDataFlowTag.PointerOrigin)
            {
                if (size <= 8)
                {
                    var currentValue = (long) (void*) ctx.Cursor.QueryMemoryBuffer<IntPtr>(address, QueryMemoryPolicy.Default);

                    Debug.Assert(currentValue == ctx.TargetValue);
                }
                else
                {
                    var currentValue = ctx.Cursor.QueryMemoryBuffer<Int128>(address, QueryMemoryPolicy.Default);

                    Debug.Assert(currentValue.Upper == (ulong) ctx.TargetValue || currentValue.Lower == (ulong) ctx.TargetValue);
                }
            }

            ctx.Cursor.AddMemoryWatchpoint(watchpoint);

            var replayResult = ctx.Cursor.ReplayBackward(Position.Min, StepCount.Max);

            ctx.Cursor.RemoveMemoryWatchpoint(watchpoint);

            if (replayResult.EventType == EventType.Process)
                throw new TtdEndOfTraceException();

            var postPosition = ctx.Cursor.GetPosition();

            replayResult = ctx.Cursor.ReplayBackward(Position.Min, 1);

            if (replayResult.EventType == EventType.Process)
                throw new TtdEndOfTraceException();

            var position = ctx.Cursor.GetPosition();

            //Note that we _don't_ need to explicitly specify the thread we're after here; it's implied by the fact that this is the thread that the event was hit on
            var registerContext = ctx.Cursor.GetCrossPlatformContext();

            ctx.Disassembler.BaseStream.Position = registerContext.IP;
            ctx.SymbolManager.Update();

            var instr = ctx.Disassembler.Disassemble();

            var name = ctx.SymbolManager.GetSymbol(registerContext);

            var dataFlowItem = new TtdDataFlowItem(ctx.TargetValue, name, ctx.Cursor.GetThreadInfo(), position, instr);
            ctx.ParentToRegisterContext[dataFlowItem] = registerContext;
            return dataFlowItem;
        }
    }
}
