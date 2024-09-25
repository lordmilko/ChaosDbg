using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an parameter that is defined on a function.
    /// </summary>
    public class CordbManagedParameterVariable : CordbManagedVariable
    {
        public int Index { get; }

        public CordbManagedParameterVariable(CorDebugVariableHome corDebugVariableHome, CordbILFrame frame, CordbModule module) : base(corDebugVariableHome, frame, module)
        {
            Index = corDebugVariableHome.ArgumentIndex;
        }

        protected override CordbValue GetValue() => CordbValue.New(Frame.CorDebugFrame.GetArgument(Index), Frame.Thread);
    }
}
