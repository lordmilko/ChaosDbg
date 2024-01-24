using System.Diagnostics;
using System.Linq;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a contiguous collection of instructions that comprise part of a function.<para/>
    /// Other chunks within the same function may directly follow this function if it ends with a
    /// non-returning instruction.
    /// </summary>
    [DebuggerDisplay("0x{StartAddress.ToString(\"X\"),nq}")]
    public class NativeFunctionChunk
    {
        public long StartAddress { get; }

        public long EndAddress { get; }

        public INativeInstruction[] Instructions { get; }

#if DEBUG
        public INativeInstruction[] Jumps => Instructions.Where(i => i.IsJump()).ToArray();
#endif

        public NativeFunctionChunk[] RefsFromThis { get; internal set; }

        public NativeFunctionChunk[] RefsToThis { get; internal set; }

        internal NativeFunctionChunk(long startAddress, INativeInstruction[] instructions)
        {
            StartAddress = startAddress;
            Instructions = instructions;

            //If we have no instructions, we'll flag it as a bad function
            if (instructions.Length > 0)
            {
                var lastInstr = instructions.Last();

                EndAddress = lastInstr.Address + lastInstr.Bytes.Length;
            }
        }

        public bool Contains(long address) =>
            address >= StartAddress && address <= EndAddress;
    }
}
