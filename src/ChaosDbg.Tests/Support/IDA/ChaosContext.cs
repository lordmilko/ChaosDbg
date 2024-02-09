using ChaosDbg.Analysis;
using ChaosDbg.Disasm;
using ChaosLib;

namespace ChaosDbg.Tests
{
    class ChaosContext
    {
        public int FunctionIndex { get; set; }

        public int InstructionIndex { get; set; }

        public INativeFunctionChunkRegion CurrentRegion { get; private set; }

        public INativeFunctionChunkRegion PreviousRegion
        {
            get
            {
                for (var i = FunctionIndex - 1; i >= 0; i--)
                {
                    if (ranges[i] is INativeFunctionChunkRegion)
                        return (INativeFunctionChunkRegion) ranges[i];
                }

                return null;
            }
        }

        public INativeInstruction CurrentInstruction => Instructions[InstructionIndex];

        public Span<INativeInstruction> Instructions => CurrentRegion.Instructions;

        private IMetadataRange[] ranges;

        public ChaosContext(IMetadataRange[] ranges)
        {
            this.ranges = ranges;
        }

        public void MoveToNextFunction()
        {
            CurrentRegion = ranges[FunctionIndex] as INativeFunctionChunkRegion;

            while (CurrentRegion == null)
            {
                FunctionIndex++;
                CurrentRegion = ranges[FunctionIndex] as INativeFunctionChunkRegion;
            }
        }
    }
}
