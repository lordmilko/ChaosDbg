using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an managed/IL frame in a stack trace.<para/>
    /// Encapsulates the <see cref="CorDebugILFrame"/> type.
    /// </summary>
    class CordbILFrame : CordbFrame<CorDebugILFrame>
    {
        public CordbILFrame(CorDebugILFrame corDebugFrame, CrossPlatformContext context) : base(corDebugFrame, context)
        {
        }
    }
}
