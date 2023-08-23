using System.Linq;

namespace ChaosDbg.Disasm
{
    static class NativeDisassemblerExtensions
    {
        /// <summary>
        /// Disassembles a single instruction from the current instruction pointer.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <returns>The disassembled instruction, or <see langword="null"/> if the disassembler read into memory that
        /// does not contain a valid instruction.</returns>
        public static INativeInstruction Disassemble(this INativeDisassembler nativeDisassembler) =>
            nativeDisassembler.EnumerateInstructions().FirstOrDefault();

        /// <summary>
        /// Disassembles a specified number of instructions from the current instruction pointer.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <param name="count">The maximum number of instructions to disassemble. Fewer than this number may be returned if the disassembler
        /// reads into memory that no longer contains valid instructions.</param>
        /// <returns>An array of disassembled instructions.</returns>
        public static INativeInstruction[] Disassemble(this INativeDisassembler nativeDisassembler, int count) =>
            nativeDisassembler.EnumerateInstructions().Take(count).ToArray();

        /// <summary>
        /// Disassembles a specified number of instructions at a given address.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <param name="address">The address to start disassembling from.</param>
        /// <param name="count">The maximum number of instructions to disassemble. Fewer than this number may be returned if the disassembler
        /// reads into memory that no longer contains valid instructions.</param>
        /// <returns>An array of disassembled instructions.</returns>
        public static INativeInstruction[] Disassemble(this INativeDisassembler nativeDisassembler, long address, int count) =>
            nativeDisassembler.EnumerateInstructions(address).Take(count).ToArray();
    }
}
