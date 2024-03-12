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

        public override CordbVariable[] Variables => throw new System.NotImplementedException();

        internal CordbNativeTransitionFrame(CordbThread thread, CrossPlatformContext context) : base(null, thread, null, context)
        {
        }
    }
}
