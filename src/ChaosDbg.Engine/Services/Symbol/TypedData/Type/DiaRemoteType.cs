using ChaosLib.TypedData;
using ClrDebug.DIA;
using static ClrDebug.HRESULT;

namespace ChaosDbg.TypedData
{
    /// <summary>
    /// Represents a type that exists in a remote process that is underpinned by a <see cref="ClrDebug.DIA.DiaSymbol"/>.
    /// </summary>
    class DiaRemoteType : IDbgRemoteType
    {
        /// <summary>
        /// Gets the <see cref="ClrDebug.DIA.DiaSymbol"/> that underpins this type.
        /// </summary>
        public DiaSymbol DiaSymbol { get; }

        public string Name => DiaSymbol.Name;
        public SymTagEnum Tag => DiaSymbol.SymTag;
        public int Length => (int) DiaSymbol.Length;
        public IDbgRemoteType BaseType
        {
            get
            {
                var baseType = DiaSymbol.Type;

                return new DiaRemoteType(baseType);
            }
        }

        public BasicType? BasicType
        {
            get
            {
                if (DiaSymbol.TryGetBaseType(out var basicType) == S_OK)
                    return basicType;

                return null;
            }
        }

        public IDbgRemoteFieldInfoCollection Fields { get; }

        public object ConstantValue { get; }

        public DiaRemoteType(DiaSymbol diaSymbol)
        {
            DiaSymbol = diaSymbol;
            Fields = new DiaRemoteFieldInfoCollection(this);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
