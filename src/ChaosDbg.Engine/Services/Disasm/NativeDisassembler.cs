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

        //Formatting relies on the symbol resolver, thus it must be part of the disassembler and cannot simply
        //be a static/standalone type
        public string Format(INativeInstruction instruction) => Formatter.Format(instruction);

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}
