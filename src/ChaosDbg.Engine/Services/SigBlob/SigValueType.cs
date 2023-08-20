using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigValueType : ISigType
    {
        mdToken Token { get; }

        string Name { get; }
    }

    class SigValueType : SigType, ISigValueType
    {
        public mdToken Token { get; }

        public string Name { get; }

        public SigValueType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            Token = reader.CorSigUncompressToken();
            Name = GetName(Token, reader.Import);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
