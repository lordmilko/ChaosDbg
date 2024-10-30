using System.Diagnostics;
using SymHelp.Symbols;
using SymHelp.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    [DebuggerDisplay("{ValueSource}")]
    public abstract class CordbNativeVariable : CordbVariable
    {
        public override string Name => Symbol.ToString();

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
            //The value knows that it might be relative to some register, but not which one. The value source knows the exact register
            return ValueSource.ToString();
        }
    }
}
