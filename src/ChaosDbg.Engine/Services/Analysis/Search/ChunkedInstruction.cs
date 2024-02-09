using ChaosDbg.Disasm;

namespace ChaosDbg.Analysis
{
    class ChunkedInstruction
    {
        public INativeInstruction Instruction { get; }

        public NativeFunctionChunkBuilder Chunk { get; set; }

#if DEBUG
        public long OriginalAddress { get; set; }
#endif
        internal ChunkedInstruction(INativeInstruction instruction)
        {
            Instruction = instruction;
        }

        public override string ToString()
        {
#if DEBUG
            return $"{OriginalAddress:X} {Instruction.ToString(DisasmFormatOptions.DbgEng.WithIP(false))}";
#else
            return Instruction.ToString();
#endif
        }
    }
}
