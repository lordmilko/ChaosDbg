using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using ChaosLib.Metadata;

namespace ChaosDbg.IL
{
    /// <summary>
    /// Represents an engine capable of disassembling Common Intermediate Language instructions.
    /// </summary>
    public class ILDisassembler
    {
        private ILDecoder Decoder { get; }

        private Stream stream;

        internal ILDisassembler(Stream stream, MetaDataProvider provider)
        {
            this.stream = stream;

            Decoder = new ILDecoder(stream, provider);
        }

        public IEnumerable<ILInstruction> EnumerateInstructions()
        {
            //Always begin from the beginning of the stream
            stream.Position = 0;

            var results = new List<ILInstruction>();

            while (true)
            {
                if (stream.Position >= stream.Length)
                    break;

                var instr = Decoder.Decode();
                results.Add(instr);
            }

            var totalOffset = 0;

            var offsetMap = new Dictionary<int, ILInstruction>();

            foreach (var instr in results)
            {
                offsetMap.Add(totalOffset, instr);

                totalOffset += instr.Length;
            }

            ILInstruction GetBranchTarget(int offset)
            {
                if (offsetMap.TryGetValue(offset, out var instr))
                    return instr;

                throw new NotImplementedException($"Could not find the instruction pointed to by offset {offset}");
            }

            ILVariable GetVariable(ILInstruction instr)
            {
                if (ILDecoder.localOpCodes.Contains(instr.OpCode))
                    return new ILVariable(ILVariableKind.Local, Convert.ToInt16(instr.Operand));

                if (ILDecoder.parameterOpCodes.Contains(instr.OpCode))
                    return new ILVariable(ILVariableKind.Parameter, Convert.ToInt16(instr.Operand));

                throw new NotImplementedException($"Don't know whether instruction {instr} is a local or parameter");
            }

            foreach (var instr in results)
            {
                switch (instr.OpCode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    case OperandType.ShortInlineBrTarget:
                        instr.Operand = GetBranchTarget((int) instr.Operand);
                        break;

                    case OperandType.InlineSwitch:
                    {
                        var existing = (int[]) instr.Operand;

                        var targets = new ILInstruction[existing.Length];

                        for (var i = 0; i < existing.Length; i++)
                            targets[i] = GetBranchTarget(existing[i]);

                        instr.Operand = targets;
                        break;
                    }

                    case OperandType.InlineVar:
                    case OperandType.ShortInlineVar:
                        instr.Operand = GetVariable(instr);
                        break;
                }
            }

            return results;
        }
    }
}
