using System;
using System.IO;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Provides facilities for creating <see cref="INativeDisassembler"/> instances
    /// from different input sources.
    /// </summary>
    public interface INativeDisassemblerProvider
    {
        /// <summary>
        /// Creates a new <see cref="INativeDisassembler"/> capable of reading instructions
        /// from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the instructions from.</param>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="INativeDisassembler"/> capable of reading the specified stream.</returns>
        INativeDisassembler CreateDisassembler(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null);

        /// <summary>
        /// Creates a <see cref="INativeDisassembler"/> capable of reading instructions from a file.<para/>
        /// The file is opened until the <see cref="INativeDisassembler"/> is disposed.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="INativeDisassembler"/> capable of reading the specified file.</returns>
        INativeDisassembler CreateDisassembler(string path, ISymbolResolver symbolResolver = null);
    }

    /// <summary>
    /// Provides facilities for creating <see cref="INativeDisassembler"/> instances
    /// from different input sources.
    /// </summary>
    public class NativeDisassemblerProvider : INativeDisassemblerProvider
    {
        /// <inheritdoc />
        public INativeDisassembler CreateDisassembler(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null)
        {
            if (stream is MemoryStream)
                throw new ArgumentException($"Cannot create an {nameof(INativeDisassembler)} using a stream of type '{nameof(MemoryStream)}'. Consider encapsulating this stream in an {nameof(AbsoluteToRelativeStream)}, specifying the module base.", nameof(stream));

            return new NativeDisassembler(stream, is32Bit, symbolResolver);
        }

        /// <inheritdoc />
        public INativeDisassembler CreateDisassembler(string path, ISymbolResolver symbolResolver = null)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var fileStream = File.OpenRead(path);
            var peFile = PEFile.FromStream(fileStream, false);

            var is32Bit = peFile.OptionalHeader.Magic == PEMagic.PE32;
            var entryPoint = peFile.OptionalHeader.AddressOfEntryPoint;

            if (entryPoint == 0)
                entryPoint = peFile.OptionalHeader.BaseOfCode;

            var rvaStream = new PERvaToPhysicalStream(fileStream, peFile);

            rvaStream.Seek(entryPoint, SeekOrigin.Begin);

            return new NativeDisassembler(rvaStream, is32Bit);            
        }
    }
}
