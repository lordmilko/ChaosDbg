using ChaosLib.Symbols;
using ChaosLib.TypedData;

namespace ChaosDbg.Cordb
{
    public class CordbNativeLocalVariable : CordbNativeVariable
    {
        public CordbNativeLocalVariable(IUnmanagedSymbol symbol, IDbgRemoteValue value) : base(symbol, value)
        {
        }
    }
}
