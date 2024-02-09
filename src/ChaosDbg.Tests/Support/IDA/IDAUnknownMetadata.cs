namespace ChaosDbg.Tests
{
    class IDAUnknownMetadata : IDAMetadata
    {
        public IDALine[] Lines { get; }

        public IDAUnknownMetadata(IDALine[] lines)
        {
            Lines = lines;
        }
    }
}