using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a local variable declared within a function.
    /// </summary>
    public class CordbManagedLocalVariable : CordbManagedVariable
    {
        public int Index { get; }

        public CordbManagedLocalVariable(CorDebugVariableHome corDebugVariableHome, CordbILFrame frame, CordbModule module) : base(corDebugVariableHome, frame, module)
        {
            Index = corDebugVariableHome.SlotIndex;
        }

        protected override CordbValue GetValue() => CordbValue.New(Frame.CorDebugFrame.GetLocalVariable(Index), Frame.Thread);
    }
}
