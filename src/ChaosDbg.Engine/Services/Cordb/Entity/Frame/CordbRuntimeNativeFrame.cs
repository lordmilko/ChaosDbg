using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a native frame that exists inside of the runtime.<para/>
    /// In the V2 stack walking API these frames most likely would have been represented as <see cref="CorDebugInternalFrame"/> types.<para/>
    /// "True" native frames are modelled using the <see cref="CordbNativeFrame"/> type.
    /// </summary>
    public class CordbRuntimeNativeFrame : CordbFrame<CorDebugNativeFrame>
    {
        protected override string DebuggerDisplay => "[Runtime] " + base.DebuggerDisplay;

        public override CordbVariable[] Variables => throw new System.NotImplementedException();

        internal CordbRuntimeNativeFrame(CorDebugNativeFrame corDebugFrame, CordbThread thread, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
        }
    }
}
