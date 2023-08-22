using System.IO;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a simple <see cref="NativeDisassembler"/> that reads instructions
    /// from a stream.
    /// </summary>
    public class NativeStreamDisassembler : NativeDisassembler
    {
        private readonly Stream stream;
        private readonly ISymbolResolver symbolResolver;

        public NativeStreamDisassembler(Stream stream, bool is32Bit, ISymbolResolver symbolResolver = null) : base(is32Bit)
        {
            this.stream = stream;
            this.symbolResolver = symbolResolver;
        }

        protected override DisasmStream CreateStream() => new RelayDisasmStream(stream);

        protected override ISymbolResolver CreateSymbolResolver() => symbolResolver;
    }
}
