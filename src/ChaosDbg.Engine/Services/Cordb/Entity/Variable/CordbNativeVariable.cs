using ChaosLib.Symbols;
using ChaosLib.TypedData;

namespace ChaosDbg.Cordb
{
    public abstract class CordbNativeVariable : CordbVariable
    {
        public IUnmanagedSymbol Symbol { get; }

        public IDbgRemoteValue Value { get; }

        protected CordbNativeVariable(IUnmanagedSymbol symbol, IDbgRemoteValue value)
        {
            Symbol = symbol;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Symbol} = {Value}";
        }
    }
}
