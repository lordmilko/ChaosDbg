using SymHelp.Symbols;
using SymHelp.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    public class CordbNativeParameterVariable : CordbNativeVariable
    {
        public CordbNativeParameterVariable(IUnmanagedSymbol symbol, ITypedValueSource valueSource) : base(symbol, valueSource)
        {
        }
    }
}
