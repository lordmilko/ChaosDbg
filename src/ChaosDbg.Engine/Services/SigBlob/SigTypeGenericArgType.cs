using ClrDebug;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a generic parameter on a member that is part of a generic type. e.g. class Foo&lt;T&gt; { void Bar(T t) {} }. The type is in fact represented as a number.
    /// </summary>
    public interface ISigTypeGenericArgType : ISigType
    {
        int Index { get; }

        string Name { get; }
    }

    /// <summary>
    /// Represents a generic parameter on a member that is part of a generic type. e.g. class Foo&lt;T&gt; { void Bar(T t) {} }. The type is in fact represented as a number.
    /// </summary>
    class SigTypeGenericArgType : SigType, ISigTypeGenericArgType
    {
        public int Index { get; }

        public string Name { get; }

        public SigTypeGenericArgType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            Index = reader.CorSigUncompressData();

            if (reader.Token.Type == CorTokenType.mdtMethodDef)
            {
                Name = GetTypeGenericArgName(Index, (mdMethodDef) reader.Token, reader.Import);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
