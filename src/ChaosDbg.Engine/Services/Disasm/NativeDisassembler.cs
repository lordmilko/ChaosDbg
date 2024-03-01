using System.Collections.Generic;
using System.IO;
using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents an engine capable of disassembling native code.
    /// </summary>
    public class NativeDisassembler : INativeDisassembler
    {
        /// <inheritdoc />
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
        public NativeDisassembler(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null)
        {
            BaseStream = stream;
            Bitness = is32Bit ? 32 : 64;

            disasmStream = new DisasmStream(stream);
            decoder = Decoder.Create(Bitness, new StreamCodeReader(disasmStream));
            formatter = symbolResolver == null ? DbgEngFormatter.Default : new DbgEngFormatter(symbolResolver);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public IEnumerable<INativeInstruction> EnumerateInstructions(long address)
        {
            //Seek to the desired address
            disasmStream.Position = address;

            return EnumerateInstructions();
        }

        //Formatting relies on the symbol resolver, thus it must be part of the disassembler and cannot simply
        //be a static/standalone type
        public string Format(INativeInstruction instruction, DisasmFormatOptions format = null) => formatter.Format(instruction, format);

        public void Format(INativeInstruction instruction, FormatterOutput formatWriter, DisasmFormatOptions format = null) => formatter.Format(instruction, formatWriter, format);

        public void Dispose()
        {
            disasmStream.Dispose();
        }
    }
}
