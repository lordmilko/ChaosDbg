using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    public class CordbNativeParameterVariable : CordbNativeVariable
    {
        public CordbNativeParameterVariable(IUnmanagedSymbol symbol, ITypedValueSource valueSource) : base(symbol, valueSource)
        {
        }
    }
}
