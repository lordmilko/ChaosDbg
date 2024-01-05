namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an unresolved transition to or from native code that may resolve to multiple
    /// <see cref="CordbNativeFrame"/> instances.
    /// </summary>
    class CordbNativeTransitionFrame : CordbFrame
    {
        public override string Name => "Transition Frame";

        public CordbNativeTransitionFrame(CrossPlatformContext context) : base(null, null, context)
        {
        }
    }
}
