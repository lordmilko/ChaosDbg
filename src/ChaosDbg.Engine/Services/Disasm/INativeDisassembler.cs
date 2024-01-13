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
        /// <param name="format">A set of options that allow customizing how the instruction is formatted.</param>
        /// <returns>The formatted instruction.</returns>
        string Format(INativeInstruction instruction, DisasmFormatOptions format = null);
    }
}
