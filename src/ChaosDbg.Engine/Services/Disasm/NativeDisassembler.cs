using System;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents an engine capable of disassembling native code.
    /// </summary>
    public abstract class NativeDisassembler : INativeDisassembler
    {
        #region Stream

        private DisasmStream stream;

        //Lazily create the stream. This is required because the implementing class may wish to use some state
        //from its constructor (e.g. a DbgEng DebugClient) to create a stream type that reads memory out
        //of the target process.
        private DisasmStream Stream
        {
            get
            {
                if (stream == null)
                    stream = CreateStream();

                return stream;
            }
        }

        protected abstract DisasmStream CreateStream();

        #endregion
        #region Decoder

        private Decoder decoder;

        //Lazily create the Decoder. This is required because the stream may need to be lazily created.
        private Decoder Decoder
        {
            get
            {
                if (decoder == null)
                    decoder = Decoder.Create(Bitness, new StreamCodeReader(Stream));

                return decoder;
            }
        }

        #endregion
        #region ISymbolResolver

        //Lazily create the symbol resolver. This is required because the implementing class may wish to use some state
        //from its constructor (e.g. a DbgEng DebugSymbols) to create a type capable of resolving symbols from an external
        //source.

        private ISymbolResolver symbolResolver;

        private ISymbolResolver SymbolResolver
        {
            get
            {
                if (symbolResolver == null)
                    symbolResolver = CreateSymbolResolver();

                return symbolResolver;
            }
        }

        protected abstract ISymbolResolver CreateSymbolResolver();

        #endregion
        #region Formatter

        //Lazily create the formatter. This is required because the symbol resolver may need to be lazily created.

        private DbgEngFormatter formatter;

        private DbgEngFormatter Formatter
        {
            get
            {
                if (formatter == null)
                    formatter = SymbolResolver == null ? DbgEngFormatter.Default : new DbgEngFormatter(SymbolResolver);

                return formatter;
            }
        }

        #endregion

        /// <summary>
        /// Gets the bitness of the target disassembly (32 or 64)
        /// </summary>
        protected int Bitness { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeDisassembler"/> class.
        /// </summary>
        /// <param name="is32Bit">Whether to disassemble 32-bit code. If false, 64-bit code is disassembled.</param>
        protected NativeDisassembler(bool is32Bit)
        {
            Bitness = is32Bit ? 32 : 64;
        }

        /// <inheritdoc />
        public INativeInstruction[] Disassemble(int count) =>
            EnumerateInstructions().Take(count).ToArray();

        /// <inheritdoc />
        public INativeInstruction[] Disassemble(long address, int count) =>
            EnumerateInstructions(address).Take(count).ToArray();

        /// <inheritdoc />
        public IEnumerable<INativeInstruction> EnumerateInstructions()
        {
            Decoder.IP = (ulong) Stream.Position;

            try
            {
                while (true)
                {
                    //Decode a single instruction. This may result in multiple reads
                    var instr = Decoder.Decode();

                    if (instr.Code == Code.INVALID)
                        yield break;

                    //Extract and clear the bytes that were read from the last operation
                    var bytes = Stream.ExtractReadBytes();

                    yield return new NativeInstruction(instr, bytes);
                }                
            }
            finally
            {
                //In case we break or throw an exception, cleanup all the bytes we haven't extracted.
                //The finally block is invoked when the IEnumerator enumerating this method is disposed
                Stream.ClearReadBytes();
            }
        }

        /// <inheritdoc />
        public IEnumerable<INativeInstruction> EnumerateInstructions(long address)
        {
            //Seek to the desired address
            Stream.Position = address;

            return EnumerateInstructions();
        }

        #region Format

        //Formatting relies on the symbol resolver, thus it must be part of the disassembler and cannot simply
        //be a static/standalone type

        //A cached StringOutput (containing a StringBuilder) to use for formatting.
        //We don't currently bother trying to pool these types, the side effect of which
        //is that formatting using NativeDisassembler is NOT thread safe
        readonly StringOutput formatWriter = new StringOutput();

        /// <summary>
        /// Formats a given instruction as a string.<para/>
        /// This method is NOT thread safe.
        /// </summary>
        /// <param name="instruction">The instruction to format.</param>
        /// <returns>The formatted instruction.</returns>
        public string Format(INativeInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            //Format the instruction in the same style as DbgEng. 

            formatWriter.Reset();

            var fmt = Formatter.Formatter;

            //Format the instruction pointer as a 32-bit or 64-bit number depending on our target bitness
            formatWriter.Write(
                Bitness == 32 ?
                    fmt.FormatInt32((int)instruction.IP, Formatter.ImmediateOptions) :
                    fmt.FormatInt64(instruction.IP, Formatter.ImmediateOptions)
            );

            formatWriter.Write(" ");

            //Convert each byte into a two digit lowercase hexadecimal value.
            foreach (var @byte in instruction.Bytes)
                formatWriter.Write(@byte.ToString("X2").ToLower());

            //Add some padding so that the instruction details are nicely aligned after the bytes
            var padding = 16 - (instruction.Bytes.Length * 2);

            for (var i = 0; i < padding; i++)
                formatWriter.Write(" ");

            //Now that we've processed the bytes, display the actual instruction
            fmt.Format(instruction.Instruction, formatWriter);
            var str = formatWriter.ToStringAndReset();

            return str;
        }

        #endregion

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}
