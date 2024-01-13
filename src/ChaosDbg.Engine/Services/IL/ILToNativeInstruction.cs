using ChaosDbg.Disasm;
using ClrDebug;

namespace ChaosDbg.IL
{
    public class ILToNativeInstruction
    {
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
