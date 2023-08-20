using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigRefType
    {
        ISigType InnerType { get; }
    }

    public class SigRefType : SigType, ISigRefType
    {
        public ISigType InnerType { get; }

        public SigRefType(ref SigReaderInternal reader) : base(CorElementType.ByRef)
        {
            InnerType = New(ref reader);
        }

        public override string ToString()
        {
            return $"{InnerType}&";
        }
    }
}
