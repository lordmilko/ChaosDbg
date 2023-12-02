using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a frame that is internal to the runtime that is on the stack. In the V3 stack walking API, these should not exist.<para/>
    /// Encapsulates the <see cref="CorDebugInternalFrame"/> type.
    /// </summary>
    class CordbInternalFrame : CordbFrame<CorDebugInternalFrame>
    {
        public CordbInternalFrame(CorDebugInternalFrame corDebugFrame, CrossPlatformContext context) : base(corDebugFrame, context)
        {
        }
    }
}
