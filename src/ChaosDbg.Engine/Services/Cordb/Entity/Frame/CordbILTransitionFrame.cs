using ClrDebug;

namespace ChaosDbg.Cordb
{
    //e.g. System.Private.CoreLib -> Interop+Kernel32.GetQueuedCompletionStatus / System.Threading.Thread.StartInternal

    /// <summary>
    /// Represents a <see cref="CorDebugNativeFrame"/> that contains a <see cref="CorDebugFunction"/>,
    /// indicating that it is most likely a P/Invoke stub or QCall.<para/>
    /// Note that while this frame may contain a <see cref="CorDebugFunction"/>, it does not contain IL code.
    /// Attempts to request Native and/or IL Code will throw <see cref="HRESULT.CORDBG_E_FUNCTION_NOT_IL"/><para/>
    /// This frame type is unrelated to <see cref="CordbNativeTransitionFrame"/>.
    /// </summary>
    public class CordbILTransitionFrame : CordbFrame<CorDebugNativeFrame>
    {
        internal CordbILTransitionFrame(CorDebugNativeFrame corDebugFrame, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, module, context)
        {
        }
    }
}
