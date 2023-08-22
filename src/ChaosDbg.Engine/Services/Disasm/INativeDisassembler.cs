using System;
using System.Collections.Generic;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents an engine capable of disassembling native code.
    /// </summary>
    public interface INativeDisassembler : IDisposable
    {
        /// <summary>
        /// Disassembles a specified number of instructions from the current instruction pointer.
        /// </summary>
        /// <param name="count">The maximum number of instructions to disassemble. Fewer than this number may be returned if the disassembler
        /// reads into memory that no longer contains valid instructions.</param>
        /// <returns>An array of disassembled instructions.</returns>
        INativeInstruction[] Disassemble(int count);

        /// <summary>
        /// Disassembles a specified number of instructions at a given address.
        /// </summary>
        /// <param name="address">The address to start disassembling from.</param>
        /// <param name="count">The maximum number of instructions to disassemble. Fewer than this number may be returned if the disassembler
        /// reads into memory that no longer contains valid instructions.</param>
        /// <returns>An array of disassembled instructions.</returns>
        INativeInstruction[] Disassemble(long address, int count);

        /// <summary>
        /// Creates an enumeration that disassembles instructions starting from the current position until
        /// an invalid instruction is encountered.
        /// </summary>
        /// <returns>An enumeration of disassembled instructions.</returns>
        IEnumerable<INativeInstruction> EnumerateInstructions();

        /// <summary>
        /// Creates an enumeration that disassembles instructions starting from the specified position until
        /// an invalid instruction is encontered.
        /// </summary>
        /// <param name="address">The address to start disassembling from.</param>
        /// <returns>An enumeration of disassembled instructions.</returns>
        IEnumerable<INativeInstruction> EnumerateInstructions(long address);

        /// <summary>
        /// Formats a given instruction as a string.
        /// </summary>
        /// <param name="instruction">The instruction to format.</param>
        /// <returns>The formatted instruction.</returns>
        string Format(INativeInstruction instruction);
    }
}
