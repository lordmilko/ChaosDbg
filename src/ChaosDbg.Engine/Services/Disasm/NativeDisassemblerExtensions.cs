using System.Diagnostics;
using System.Linq;
using ChaosDbg.Analysis;

namespace ChaosDbg.Disasm
{
    public static class NativeDisassemblerExtensions
    {
        #region INativeDisassembler

        /// <summary>
        /// Disassembles a single instruction from the current instruction pointer.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <returns>The disassembled instruction, or <see langword="null"/> if the disassembler read into memory that
        /// does not contain a valid instruction.</returns>
        public static INativeInstruction Disassemble(this NativeDisassembler nativeDisassembler) =>
            nativeDisassembler.EnumerateInstructions().FirstOrDefault();

        /// <summary>
        /// Disassembles a single instruction at a given address.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <param name="address">The address to start disassembling from.</param>
        /// <returns>The disassembled instruction, or <see langword="null"/> if the disassembler read into memory that
        /// does not contain a valid instruction.</returns>
        public static INativeInstruction Disassemble(this NativeDisassembler nativeDisassembler, long address) =>
            nativeDisassembler.EnumerateInstructions(address).FirstOrDefault();

        /// <summary>
        /// Disassembles a specified number of instructions at a given address.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <param name="address">The address to start disassembling from.</param>
        /// <param name="count">The maximum number of instructions to disassemble. Fewer than this number may be returned if the disassembler
        /// reads into memory that no longer contains valid instructions.</param>
        /// <returns>An array of disassembled instructions.</returns>
        public static INativeInstruction[] Disassemble(this NativeDisassembler nativeDisassembler, long address, int count) =>
            nativeDisassembler.EnumerateInstructions(address).Take(count).ToArray();

        [DebuggerStepThrough]
        public static NativeCodeRegionCollection DisassembleCodeRegions(this NativeDisassembler nativeDisassembler, long address, DisasmFunctionResolutionContext context = null) =>
            new NativeCodeRegionDisassembler(nativeDisassembler, address, context).Disassemble();

        #endregion
    }
}
