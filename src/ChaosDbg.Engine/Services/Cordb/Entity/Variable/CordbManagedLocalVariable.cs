using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a local variable declared within a function.
    /// </summary>
    public class CordbManagedLocalVariable : CordbManagedVariable
    {
        public int Index { get; }

        public CordbManagedLocalVariable(CorDebugVariableHome corDebugVariableHome, CordbModule module) : base(corDebugVariableHome, module)
        {
            Index = corDebugVariableHome.SlotIndex;
        }
    }
}
