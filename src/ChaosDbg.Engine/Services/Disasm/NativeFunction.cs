using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosLib.Metadata;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a complete disassembled native function.
    /// </summary>
    public class NativeFunction
    {
        /// <summary>
        /// Gets the start address of the function.
        /// </summary>
        public long Address { get; }

        /// <summary>
        /// Gets the symbol information that is available for this function.<para/>
        /// May be null if no information is available.
        /// </summary>
        public ISymbol Symbol { get; }

        /// <summary>
        /// Gets the disjoint ranges that this function's instructions were split across.
        /// A range is defined as a series of instructions that is terminated by an instruction
        /// that cannot be crossed, such as a return, unconditional jump or fail fast interrupt.<para/>
        /// Chunks may or may not be contiguous.
        /// </summary>
        public NativeFunctionChunk[] Chunks { get; set; }

        /// <summary>
        /// Gets all instructions contained in this function's function chunks.
        /// </summary>
        public INativeInstruction[] Instructions => Chunks.SelectMany(v => v.Instructions).ToArray();

        public INativeInstruction[] Calls => Instructions.Where(v => v.Instruction.Mnemonic == Mnemonic.Call).ToArray();

        public INativeInstruction[] Jumps => Instructions.Where(v => v.IsJump()).ToArray();

        public INativeInstruction[] ExternalJumps
        {
            get
            {
                //Normally, any jmp instructions will be followed and be part of some chunk. This is not the case
                //when you have a jmp whose operand is a register or memory address

                var externalJumps = new List<INativeInstruction>();

                foreach (var jump in Jumps)
                {
                    if (jump.TryGetOperand(out var operand) && !Contains(operand))
                        externalJumps.Add(jump);
                }

                return externalJumps.ToArray();
            }
        }

        public NativeFunction(long address, ISymbol symbol, NativeFunctionChunk[] chunks)
        {
            Address = address;
            Symbol = symbol;
            Chunks = chunks;
        }

        public bool Contains(long address) => Chunks.Any(c => c.Contains(address));

        public NativeFunctionChunk GetChunkContainingAddress(long address) =>
            Chunks.FirstOrDefault(c => c.Contains(address));

        public IFunctionId Source { get; set; } //temp

        public override string ToString()
        {
            if (Symbol == null)
                return "0x" + Address.ToString("x16");

            return Symbol.ToString();
        }
    }
}
