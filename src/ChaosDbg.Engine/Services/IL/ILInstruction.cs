﻿using System.Collections;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace ChaosDbg.IL
{
    /// <summary>
    /// Represents a Common Intermediate Language instruction and its corresponding bytes.
    /// </summary>
    public class ILInstruction
    {
        public int Offset { get; }

        public int Length { get; }

        public OpCode OpCode { get; }

        public object Operand { get; internal set; }

        public OpCodeKind Kind => (OpCodeKind) (OpCode.Value & 0xffff);

        public ILInstruction(int offset, int length, OpCode opcode, object operand)
        {
            Offset = offset;
            Length = length;
            OpCode = opcode;
            Operand = operand;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append($"IL_{Offset:X4}   {OpCode}");

            if (Operand != null)
            {
                builder.Append(" ");

                if (Operand is ILInstruction i)
                    builder.Append($"IL_{i.Offset:X4}");
                else if (Operand != null && Operand.GetType().IsArray)
                {
                    var elmType = Operand.GetType().GetElementType();

                    if (elmType == typeof(ILInstruction))
                        builder.Append($"({string.Join(", ", ((ILInstruction[]) Operand).Select(v => $"IL_{v.Offset:X4}"))}");
                    else
                        builder.Append($"({string.Join(", ", ((IEnumerable) Operand).Cast<object>())})");
                }
                else
                    builder.Append(Operand);
            }

            return builder.ToString();
        }
    }
}
