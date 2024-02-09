using System;
using ChaosDbg.Disasm;

namespace ChaosDbg.Analysis
{
    public interface INativeXRefInfo
    {
        NativeXRefKind Kind { get; }
    }

    public class NativeJumpXRefInfo : INativeXRefInfo
    {
        public XRefAwareNativeInstruction TargetInstruction { get; }

        public INativeFunctionChunkRegion TargetRegion { get; }

        public NativeXRefKind Kind { get; }

        internal NativeJumpXRefInfo(
            XRefAwareNativeInstruction targetInstruction,
            INativeFunctionChunkRegion targetRegion,
            NativeXRefKind kind)
        {
            if (targetInstruction == null)
                throw new ArgumentNullException(nameof(targetInstruction));

            if (targetRegion == null)
                throw new ArgumentNullException(nameof(targetInstruction));

            TargetInstruction = targetInstruction;
            TargetRegion = targetRegion;
            Kind = kind;
        }
    }

    public class NativeDataXRefInfo : INativeXRefInfo
    {
        public NativeXRefKind Kind { get; }
    }

    public class NativeCallXRefInfo : INativeXRefInfo
    {
        public IMetadataRange Target { get; }

        public NativeXRefKind Kind => NativeXRefKind.Call;

        internal NativeCallXRefInfo(IMetadataRange target)
        {
            //This could be an import, an XFG guard, etc
            Target = target;
        }

        public override string ToString()
        {
            return Target.ToString();
        }
    }
}
