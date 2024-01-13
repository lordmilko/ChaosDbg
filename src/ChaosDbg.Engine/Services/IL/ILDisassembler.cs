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

            object GetBranchTarget(int offset)
            {
                var totalOffset = 0;

                foreach (var instr in results)
                {
                    if (totalOffset == offset)
                        return instr;

                    totalOffset += instr.Length;
                }

                throw new NotImplementedException($"Could not find the instruction pointed to by offset {offset}");
            }

            foreach (var instr in results)
            {
                switch (instr.OpCode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    case OperandType.ShortInlineBrTarget:
                        instr.Operand = GetBranchTarget((int) instr.Operand);
                        break;
                }
            }

            return results;
        }
    }
}
