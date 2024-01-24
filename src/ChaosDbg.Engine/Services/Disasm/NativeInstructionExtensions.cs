using System;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    static class NativeInstructionExtensions
    {
        public static bool TryGetOperand(this INativeInstruction instruction, out long operand)
        {
            var icedInstr = instruction.Instruction;

            switch (icedInstr.Op0Kind)
            {
                case OpKind.NearBranch32:
                case OpKind.NearBranch64:
                    operand = (long) icedInstr.NearBranchTarget;
                    return true;

                case OpKind.Memory:
                    if (icedInstr.MemoryBase != Register.EIP && icedInstr.MemoryBase != Register.RIP)
                        break;

                    if (icedInstr.MemoryDisplSize == 8)
                        operand = (long) icedInstr.MemoryDisplacement64;
                    else
                        operand = icedInstr.MemoryDisplacement32;

                    return true;

                case OpKind.Register:
                    break; //No value to get

                default:
                    throw new NotImplementedException($"Don't know how to handle an operand of type '{icedInstr.Op0Kind}'");
            }

            operand = default;
            return false;
        }

        public static bool IsJump(this INativeInstruction instruction)
        {
            switch (instruction.Instruction.FlowControl)
            {
                case FlowControl.ConditionalBranch:
                case FlowControl.UnconditionalBranch:
                case FlowControl.IndirectBranch:
                    return true;

                default:
                    return false;
            }
        }
    }
}
