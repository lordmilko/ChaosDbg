namespace ChaosDbg.Metadata
{
    public interface ISigFnPtrParameter : ISigParameter
    {
    }

    public class SigFnPtrParameter : ISigFnPtrParameter
    {
        public ISigType Type { get; }

        internal SigFnPtrParameter(SigType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
