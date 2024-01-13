namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an unresolved transition to or from native code that may resolve to multiple
    /// <see cref="CordbNativeFrame"/> instances.<para/>
    /// This frame type is unrelated to <see cref="CordbILTransitionFrame"/>.
    /// </summary>
    public class CordbNativeTransitionFrame : CordbFrame
    {
        public override string Name => "Transition Frame";

        internal CordbNativeTransitionFrame(CrossPlatformContext context) : base(null, null, context)
        {
        }
    }
}
