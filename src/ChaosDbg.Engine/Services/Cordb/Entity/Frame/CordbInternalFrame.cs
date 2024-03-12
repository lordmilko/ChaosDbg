using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a frame that is internal to the runtime that is on the stack. In the V3 stack walking API, these should not exist.<para/>
    /// Encapsulates the <see cref="CorDebugInternalFrame"/> type.
    /// </summary>
    public class CordbInternalFrame : CordbFrame<CorDebugInternalFrame>
    {
        public override CordbVariable[] Variables => throw new System.NotImplementedException();

        internal CordbInternalFrame(CorDebugInternalFrame corDebugFrame, CordbThread thread, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
        }
    }
}
