using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a native frame that exists inside of the runtime.<para/>
    /// In the V2 stack walking API these frames most likely would have been represented as <see cref="CorDebugInternalFrame"/> types.<para/>
    /// "True" native frames are modelled using the <see cref="CordbNativeFrame"/> type.
    /// </summary>
    class CordbRuntimeNativeFrame : CordbFrame<CorDebugNativeFrame>
    {
        protected override string DebuggerDisplay => "[Runtime] " + base.DebuggerDisplay;

        public CordbRuntimeNativeFrame(CorDebugNativeFrame corDebugFrame, CrossPlatformContext context) : base(corDebugFrame, context)
        {
        }
    }
}
