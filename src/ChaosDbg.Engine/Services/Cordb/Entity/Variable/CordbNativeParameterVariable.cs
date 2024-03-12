using ChaosLib.Metadata;
using ChaosLib.TypedData;

namespace ChaosDbg.Cordb
{
    public class CordbNativeParameterVariable : CordbNativeVariable
    {
        public CordbNativeParameterVariable(IUnmanagedSymbol symbol, IDbgRemoteValue value) : base(symbol, value)
        {
        }
    }
}
