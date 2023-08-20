using ClrDebug;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a generic parameter in generic method definition. The type is in fact represented as a number.
    /// </summary>
    public interface ISigMethodGenericArgType : ISigType
    {
        string Name { get; }

        int Index { get; }
    }

    /// <summary>
    /// Represents a generic parameter in generic method definition. The type is in fact represented as a number.
    /// </summary>
    class SigMethodGenericArgType : SigType, ISigMethodGenericArgType
    {
        public string Name { get; }

        public int Index { get; }

        public SigMethodGenericArgType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            Index = reader.CorSigUncompressData();

            if (reader.Token.Type == CorTokenType.mdtMethodDef)
            {
                Name = GetMethodGenericArgName(Index, (mdMethodDef) reader.Token, reader.Import);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
