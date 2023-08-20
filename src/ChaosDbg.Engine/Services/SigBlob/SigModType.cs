using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigModType
    {
        mdToken Token { get; }

        string Name { get; }

        ISigType InnerType { get; }
    }

    public class SigModType : SigType, ISigModType
    {
        public mdToken Token { get; }

        public string Name { get; }

        public ISigType InnerType { get; }

        public SigModType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            Token = reader.CorSigUncompressToken();
            Name = GetName(Token, reader.Import);
            InnerType = New(ref reader);
        }

        public override string ToString()
        {
            return $"{Name} {InnerType}";
        }
    }
}
