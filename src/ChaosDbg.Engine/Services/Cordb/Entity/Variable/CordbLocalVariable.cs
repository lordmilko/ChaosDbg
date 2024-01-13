using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a local variable declared within a function.
    /// </summary>
    public class CordbLocalVariable : CordbVariable
    {
        public int Index { get; }

        public CordbLocalVariable(CorDebugVariableHome corDebugVariableHome, CordbModule module) : base(corDebugVariableHome, module)
        {
            Index = corDebugVariableHome.SlotIndex;
        }
    }
}
