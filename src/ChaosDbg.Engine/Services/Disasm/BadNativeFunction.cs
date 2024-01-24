using System.Diagnostics;
using ChaosLib.Metadata;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a native function that could not properly be disassembled.
    /// </summary>
    [DebuggerDisplay("[Bad] {ToString(),nq}")]
    public class BadNativeFunction
    {
        public long Address { get; }
        public ISymbol Symbol { get; }

        /// <summary>
        /// Gets the reason the function was identified as being bad.
        /// </summary>
        public BadFunctionReason Reason { get; }

        /// <summary>
        /// Gets the chunks that were read before the disassembler gave up.
        /// </summary>
        public NativeFunctionChunk[] Chunks { get; }

        public BadNativeFunction(long address, ISymbol symbol, BadFunctionReason reason, NativeFunctionChunk[] chunks)
        {
            Address = address;
            Symbol = symbol;
            Reason = reason;
            Chunks = chunks;
        }

        public override string ToString()
        {
            if (Symbol == null)
                return "0x" + Address.ToString("x16");

            return Symbol.ToString();
        }
    }
}
