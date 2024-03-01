using System.Diagnostics;
using System.Text;
using ChaosDbg.Disasm;
using ClrDebug;

namespace ChaosDbg.IL
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class ILToNativeInstruction
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                builder.Append($"[{Kind}] ");

                if (IL.Length > 0)
                {
                    builder.Append(IL[0]);

                    if (NativeInstructions.Length > 0)
                        builder.Append(" / ");
                }

                if (NativeInstructions.Length > 0)
                {
                    builder.Append(NativeInstructions[0].Instruction);
                }

                return builder.ToString();
            }
        }
        
        public ILToNativeInstructionKind Kind { get; }

        public ILInstruction[] IL { get; }

        public INativeInstruction[] NativeInstructions { get; }

        public COR_DEBUG_IL_TO_NATIVE_MAP Mapping { get; }

        public ILToNativeInstruction(ILToNativeInstructionKind kind, ILInstruction[] il, INativeInstruction[] nativeInstructions, COR_DEBUG_IL_TO_NATIVE_MAP mapping)
        {
            Kind = kind;
            IL = il;
            NativeInstructions = nativeInstructions;
            Mapping = mapping;
        }
    }
}
