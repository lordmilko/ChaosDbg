using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a "true" native frame in a call stack.<para/>
    /// Instances of this type are not backed by a <see cref="CorDebugFrame"/>. Rather, they
    /// have been completely "synthesized" by performing a manual stack walk where the V3 stack walker has informed
    /// us that one or more native frames should exist.
    /// </summary>
    public class CordbNativeFrame : CordbFrame
    {
        /// <summary>
        /// Gets the display name of this frame.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Gets the symbol that is associated with this frame, if one exists.
        /// </summary>
        public IDisplacedSymbol Symbol { get; }

        internal CordbNativeFrame(NativeFrame nativeFrame, CordbModule module) : base(null, module, nativeFrame.Context)
        {
            string name;

            if (nativeFrame.FunctionName != null)
                name = nativeFrame.FunctionName;
            else if (nativeFrame.Module != null)
                name = $"{nativeFrame.Module}!{nativeFrame.IP:X}";
            else
                name = null;

            Name = name;
            Symbol = nativeFrame.Symbol;
        }
    }
}
