using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an parameter that is defined on a function.
    /// </summary>
    public class CordbManagedParameterVariable : CordbManagedVariable
    {
        public int Index { get; }

        public CordbManagedParameterVariable(CorDebugVariableHome corDebugVariableHome, CordbModule module) : base(corDebugVariableHome, module)
        {
            Index = corDebugVariableHome.ArgumentIndex;
        }
    }
}
