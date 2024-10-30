using System;
using System.Collections.Generic;
using System.IO;
using ChaosLib.Memory;
using Iced.Intel;
using PESpy;
using Decoder = Iced.Intel.Decoder;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents an engine capable of disassembling native code.
    /// </summary>
    public class NativeDisassembler
    {
        #region Static

        /// <summary>
        /// Creates a <see cref="NativeDisassembler"/> capable of reading instructions from a file.<para/>
        /// The file is opened until the <see cref="NativeDisassembler"/> is disposed.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="NativeDisassembler"/> capable of reading the specified file.</returns>
        public static NativeDisassembler FromPath(string path, ISymbolResolver symbolResolver = null)
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

            return new NativeDisassembler(rvaStream, is32Bit, symbolResolver);
        }

        /// <summary>
        /// Creates a new <see cref="NativeDisassembler"/> capable of reading instructions
        /// from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the instructions from.</param>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="NativeDisassembler"/> capable of reading the specified stream.</returns>
        public static NativeDisassembler FromStream(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null)
        {
            if (stream is MemoryStream)
                throw new ArgumentException($"Cannot create an {nameof(NativeDisassembler)} using a stream of type '{nameof(MemoryStream)}'. Consider encapsulating this stream in an {nameof(AbsoluteToRelativeStream)}, specifying the module base.", nameof(stream));

            return new NativeDisassembler(stream, is32Bit, symbolResolver);
        }

        /// <summary>
        /// Creates a new <see cref="NativeDisassembler"/> capable of reading instructions from a byte array.
        /// </summary>
        /// <param name="bytes">The byte array of instructions to be disassembled.</param>
        /// <param name="baseAddress">The base address that the instructions in the byte array start from.</param>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        /// <returns>A <see cref="NativeDisassembler"/> capable of reading the specified byte array.</returns>
        public static NativeDisassembler FromByteArray(
            byte[] bytes,
            long baseAddress,
            bool is32Bit,
            ISymbolResolver symbolResolver = null) => FromStream(new AbsoluteToRelativeStream(new MemoryStream(bytes), baseAddress), is32Bit, symbolResolver);

        #endregion

        /// <summary>
        /// Gets the underlying stream of this <see cref="NativeDisassembler"/>.
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Gets or sets the current position of the instruction pointer,
        /// controlling the address of the instruction that will next be disassembled.
        /// </summary>
        public long IP
        {
            get => disasmStream.Position;
            set
            {
                disasmStream.Position = value;
                decoder.IP = (ulong) value;
            }
        }

        /// <summary>
        /// Gets the bitness of the target disassembly (32 or 64)
        /// </summary>
        protected int Bitness { get; }

        private readonly DisasmStream disasmStream;
        private readonly Decoder decoder;
        private readonly DbgEngFormatter formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeDisassembler"/> class.
        /// </summary>
        /// <param name="stream">The stream to read the instructions from.</param>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        /// <param name="symbolResolver">A type capable of reading symbols for the instructions found in the stream.
        /// If <see langword="null"/>, symbols will not be resolved.</param>
        private NativeDisassembler(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null)
        {
            BaseStream = stream;
            Bitness = is32Bit ? 32 : 64;

            disasmStream = new DisasmStream(stream);
            decoder = Decoder.Create(Bitness, new StreamCodeReader(disasmStream));
            formatter = symbolResolver == null ? DbgEngFormatter.Default : new DbgEngFormatter(symbolResolver);
        }

        /// <summary>
        /// Creates an enumeration that disassembles instructions starting from the current position until
        /// an invalid instruction is encountered.
        /// </summary>
        /// <returns>An enumeration of disassembled instructions.</returns>
        public IEnumerable<INativeInstruction> EnumerateInstructions()
        {
            decoder.IP = (ulong) disasmStream.Position;

            try
            {
                while (true)
                {
                    //Decode a single instruction. This may result in multiple reads
                    var instr = decoder.Decode();

                    //We've encountered a bad instruction! Is it because we're at the end of the stream?
                    if (instr.Code == Code.INVALID)
                    {
                        var previousInstr = instr;
                        var previousInstrBytes = disasmStream.ExtractReadBytes();

                        //Read another instruction to see whether we progress or not
                        instr = decoder.Decode();

                        //The stream didn't move, we must be at the end. Don't emit either of the bad instructions, just break
                        if (instr.IP == previousInstr.IP)
                            yield break;

                        //The stream moved. Write out the previous instruction followed by the one we just read
                        yield return new NativeInstruction(previousInstr, previousInstrBytes, formatter);

                        //Fall through to return the second instruction we read as well
                    }

                    //Extract and clear the bytes that were read from the last operation
                    var bytes = disasmStream.ExtractReadBytes();

                    yield return new NativeInstruction(instr, bytes, formatter);
                }                
            }
            finally
            {
                //In case we break or throw an exception, cleanup all the bytes we haven't extracted.
                //The finally block is invoked when the IEnumerator enumerating this method is disposed
                disasmStream.ClearReadBytes();
            }
        }

        /// <summary>
        /// Creates an enumeration that disassembles instructions starting from the specified position until
        /// an invalid instruction is encontered.
        /// </summary>
        /// <param name="address">The address to start disassembling from.</param>
        /// <returns>An enumeration of disassembled instructions.</returns>
        public IEnumerable<INativeInstruction> EnumerateInstructions(long address)
        {
            if (address == 0)
                throw new ArgumentException("Address should not be 0", nameof(address));

            //Seek to the desired address
            disasmStream.Position = address;

            return EnumerateInstructions();
        }

        //Formatting relies on the symbol resolver, thus it must be part of the disassembler and cannot simply
        //be a static/standalone type

        /// <summary>
        /// Formats a given instruction as a string.
        /// </summary>
        /// <param name="instruction">The instruction to format.</param>
        /// <param name="format">A set of options that allow customizing how the instruction is formatted.</param>
        /// <returns>The formatted instruction.</returns>
        public string Format(INativeInstruction instruction, DisasmFormatOptions format = null) => formatter.Format(instruction, format);

        public void Format(INativeInstruction instruction, FormatterOutput formatWriter, DisasmFormatOptions format = null) => formatter.Format(instruction, formatWriter, format);

        public void Dispose()
        {
            disasmStream.Dispose();
        }
    }
}
