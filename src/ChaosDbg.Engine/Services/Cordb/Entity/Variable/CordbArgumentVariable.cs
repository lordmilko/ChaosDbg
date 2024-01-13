using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an argument that is passed into a function.
    /// </summary>
    public class CordbArgumentVariable : CordbVariable
    {
        public int Index { get; }

        public CordbArgumentVariable(CorDebugVariableHome corDebugVariableHome, CordbModule module) : base(corDebugVariableHome, module)
        {
            Index = corDebugVariableHome.ArgumentIndex;
        }
    }
}
