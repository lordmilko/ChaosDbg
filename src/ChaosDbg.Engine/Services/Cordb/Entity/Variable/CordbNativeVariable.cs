using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    public abstract class CordbNativeVariable : CordbVariable
    {
        public IUnmanagedSymbol Symbol { get; }

        public ITypedValueSource ValueSource { get; }

        public ITypedValue Value => ValueSource.Value;

        protected CordbNativeVariable(IUnmanagedSymbol symbol, ITypedValueSource valueSource)
        {
            Symbol = symbol;
            ValueSource = valueSource;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
