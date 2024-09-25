using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosDbg.Cordb;
using ChaosLib;
using ChaosLib.Memory;
using Iced.Intel;

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
        public static INativeInstruction Disassemble(this INativeDisassembler nativeDisassembler) =>
            nativeDisassembler.EnumerateInstructions().FirstOrDefault();

        /// <summary>
        /// Disassembles a single instruction at a given address.
        /// </summary>
        /// <param name="nativeDisassembler">The disassembler to disassemble instructions from.</param>
        /// <param name="address">The address to start disassembling from.</param>
        /// <returns>The disassembled instruction, or <see langword="null"/> if the disassembler read into memory that
        /// does not contain a valid instruction.</returns>
        public static INativeInstruction Disassemble(this INativeDisassembler nativeDisassembler, long address) =>
            nativeDisassembler.EnumerateInstructions(address).FirstOrDefault();

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

        [DebuggerStepThrough]
        public static NativeCodeRegionCollection DisassembleCodeRegions(this INativeDisassembler nativeDisassembler, long address, DisasmFunctionResolutionContext context = null) =>
            new NativeCodeRegionDisassembler(nativeDisassembler, address, context).Disassemble();

        #endregion
        #region INativeDisassemblerProvider

        /// <summary>
        /// Creates a new <see cref="INativeDisassembler"/> capable of reading instructions from a byte array.
        /// </summary>
        /// <param name="nativeDisassemblerProvider">The <see cref="INativeDisassemblerProvider"/> that should be used to create the <see cref="INativeDisassembler"/></param>
        /// <param name="bytes">The byte array of instructions to be disassembled.</param>
        /// <param name="baseAddress">The base address that the instructions in the byte array start from.</param>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="INativeDisassembler"/> capable of reading the specified byte array.</returns>
        public static INativeDisassembler CreateDisassembler(
            this INativeDisassemblerProvider nativeDisassemblerProvider,
            byte[] bytes,
            long baseAddress,
            bool is32Bit,
            ISymbolResolver symbolResolver = null) => nativeDisassemblerProvider.CreateDisassembler(new AbsoluteToRelativeStream(new MemoryStream(bytes), baseAddress), is32Bit, symbolResolver);

        public static INativeDisassembler CreateDisassembler(
            this INativeDisassemblerProvider nativeDisassemblerProvider,
            CordbProcess process,
            ISymbolResolver symbolResolver = null)
        {
            var stream = new MemoryReaderStream((IMemoryReader) process.DataTarget);

            return nativeDisassemblerProvider.CreateDisassembler(stream, process.Is32Bit, symbolResolver);
        }

        #endregion
    }
}
