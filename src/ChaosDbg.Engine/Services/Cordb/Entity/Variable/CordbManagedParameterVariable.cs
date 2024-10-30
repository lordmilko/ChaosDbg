using ClrDebug;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an parameter that is defined on a function.
    /// </summary>
    public class CordbManagedParameterVariable : CordbManagedVariable
    {
        public int Index => CorDebugVariableHome.ArgumentIndex;

        public CordbManagedParameterVariable(
            CorDebugVariableHome corDebugVariableHome,
            ManagedParameterSymbol symbol,
            CordbILFrame frame,
            CordbModule module,
            CORDB_ADDRESS startAddress,
            CORDB_ADDRESS endAddress) : base(corDebugVariableHome, symbol, frame, module, startAddress, endAddress)
        {
        }

        protected override CordbValue GetValue() => CordbValue.New(Frame.CorDebugFrame.GetArgument(Index), Frame.Thread);
    }
}
