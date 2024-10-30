using ClrDebug;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a local variable declared within a function.
    /// </summary>
    public class CordbManagedLocalVariable : CordbManagedVariable
    {
        public int Index => CorDebugVariableHome.SlotIndex;

        public CordbManagedLocalVariable(
            CorDebugVariableHome corDebugVariableHome,
            ManagedLocalSymbol symbol,
            CordbILFrame frame,
            CordbModule module,
            CORDB_ADDRESS startAddress,
            CORDB_ADDRESS endAddress) : base(corDebugVariableHome, symbol, frame, module, startAddress, endAddress)
        {
        }

        protected override CordbValue GetValue() => CordbValue.New(Frame.CorDebugFrame.GetLocalVariable(Index), Frame.Thread);
    }
}
