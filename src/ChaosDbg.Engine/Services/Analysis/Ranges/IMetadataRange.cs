namespace ChaosDbg.Analysis
{
    public interface IMetadataRange
    {
        long StartAddress { get; }

        long EndAddress { get; }
    }
}
