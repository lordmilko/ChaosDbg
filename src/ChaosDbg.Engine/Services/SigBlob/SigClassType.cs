using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigClassType : ISigType
    {
        string Name { get; }

        mdToken Token { get; }
    }

    class SigClassType : SigType, ISigClassType
    {
        public string Name { get; }

        public mdToken Token { get; }

        public SigClassType(CorElementType type, ref SigReaderInternal reader) : base(type)
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
