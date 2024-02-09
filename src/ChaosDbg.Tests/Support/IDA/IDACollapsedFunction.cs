namespace ChaosDbg.Tests
{
    class IDACollapsedFunction : IDAMetadata
    {
        public string Name { get; }

        public int Size { get; }

        public IDACollapsedFunction(string name, int size)
        {
            Name = name;
            Size = size;
        }

        public override string ToString()
        {
            return $"{Name} (Collapsed)";
        }
    }
}