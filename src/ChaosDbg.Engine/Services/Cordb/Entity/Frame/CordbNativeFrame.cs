using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a "true" native frame in a call stack.<para/>
    /// Instances of this type are not backed by a <see cref="CorDebugFrame"/>. Rather, they
    /// have been completely "synthesized" by performing a manual stack walk where the V3 stack walker has informed
    /// us that one or more native frames should exist.
    /// </summary>
    class CordbNativeFrame : CordbFrame
    {
        public override string Name { get; }

        public CordbNativeFrame(NativeFrame nativeFrame) : base(null, nativeFrame.Context)
        {
            string name;

            if (nativeFrame.FunctionName != null)
                name = $"{nativeFrame.ModuleName}!{nativeFrame.FunctionName}";
            else if (nativeFrame.ModuleName != null)
                name = $"{nativeFrame.ModuleName}!{nativeFrame.IP:X}";
            else
                name = null;

            Name = name;
        }
    }
}
