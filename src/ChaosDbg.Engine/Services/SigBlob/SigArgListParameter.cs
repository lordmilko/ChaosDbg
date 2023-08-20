namespace ChaosDbg.Metadata
{
    public interface ISigArgListParameter : ISigParameter
    {
    }

    public class SigArgListParameter : ISigArgListParameter
    {
        public ISigType Type { get; }

        public override string ToString()
        {
            return "__arglist";
        }
    }
}
