using System;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Provides facilities for performing custom disassembly formatting in conjunction with <see cref="DbgEngFormatter"/>.
    /// </summary>
    class DbgEngOptionsProvider : IFormatterOptionsProvider
    {
        public static readonly DbgEngOptionsProvider Instance = new DbgEngOptionsProvider();

        private DbgEngOptionsProvider()
        {
        }

        public void GetOperandOptions(in Instruction instruction, int operand, int instructionOperand,
            ref FormatterOperandOptions options, ref NumberFormattingOptions numberOptions)
        {
            if (operand != instructionOperand)
                throw new NotImplementedException($"We're assuming operand and instruction operand are always the same, and are using 'operand' in our code, but operand was {operand} and instructionOperand was {instructionOperand}.");

            var kind = instruction.GetOpKind(operand);

            //In DbgEng, we want to show operands in uppercase hex, but addresses in lowercase hex
            switch (kind)
            {
                case OpKind.Immediate8:
                case OpKind.Immediate8_2nd:
                case OpKind.Immediate16:
                case OpKind.Immediate32:
                case OpKind.Immediate64:
                case OpKind.Immediate8to16:
                case OpKind.Immediate8to32:
                case OpKind.Immediate8to64:
                    numberOptions.UppercaseHex = true;
                    break;
            }
        }
    }
}
