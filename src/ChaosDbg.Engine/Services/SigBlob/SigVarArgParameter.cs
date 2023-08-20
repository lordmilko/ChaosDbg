namespace ChaosDbg.Metadata
{
    public interface ISigVarArgParameter : ISigParameter
    {
    }

    public class SigVarArgParameter : ISigVarArgParameter
    {
        public ISigType Type { get; }

        internal SigVarArgParameter(SigType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
