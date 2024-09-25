using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    public class CordbNativeLocalVariable : CordbNativeVariable
    {
        public CordbNativeLocalVariable(IUnmanagedSymbol symbol, ITypedValueSource valueSource) : base(symbol, valueSource)
        {
        }
    }
}
