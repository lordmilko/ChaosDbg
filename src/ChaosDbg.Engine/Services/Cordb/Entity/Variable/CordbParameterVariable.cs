using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an parameter that is defined on a function.
    /// </summary>
    public class CordbParameterVariable : CordbVariable
    {
        public int Index { get; }

        public CordbParameterVariable(CorDebugVariableHome corDebugVariableHome, CordbModule module) : base(corDebugVariableHome, module)
        {
            Index = corDebugVariableHome.ArgumentIndex;
        }
    }
}
