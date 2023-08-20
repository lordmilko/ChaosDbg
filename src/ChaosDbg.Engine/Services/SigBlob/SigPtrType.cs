using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigPtrType
    {
        ISigType PtrType { get; }
    }

    class SigPtrType : SigType, ISigPtrType
    {
        public ISigType PtrType { get; }

        public SigPtrType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            PtrType = New(ref reader);
        }

        public override string ToString()
        {
            return $"{PtrType}*";
        }
    }
}
