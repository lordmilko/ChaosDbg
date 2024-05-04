using ChaosLib.TypedData;
using ClrDebug.DIA;

namespace ChaosDbg.TypedData
{
    class DiaRemoteFieldInfo : IDbgRemoteFieldInfo
    {
        public string Name { get; }
        public int? Offset => FieldSymbol.Offset;
        public IDbgRemoteType Type { get; }

        public DiaSymbol FieldSymbol { get; }

        public DiaRemoteFieldInfo(DiaSymbol type)
        {
            FieldSymbol = type;
            Type = new DiaRemoteType(type.Type);
        }
    }
}
