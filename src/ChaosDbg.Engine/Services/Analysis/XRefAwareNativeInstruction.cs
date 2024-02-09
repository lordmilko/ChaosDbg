using ChaosDbg.Disasm;
using Iced.Intel;
using System.Collections.Generic;

namespace ChaosDbg.Analysis
{
    /// <summary>
    /// Represents a <see cref="NativeInstruction"/> that is XRef'd either to or from another instruction.
    /// </summary>
    public class XRefAwareNativeInstruction : NativeInstruction
    {
        public List<INativeXRefInfo> RefsFromThis { get; } = new List<INativeXRefInfo>();

        public List<INativeXRefInfo> RefsToThis { get; } = new List<INativeXRefInfo>();

        internal XRefAwareNativeInstruction(in Instruction instruction, byte[] bytes, DbgEngFormatter formatter) : base(instruction, bytes, formatter)
        {
        }
    }
}
