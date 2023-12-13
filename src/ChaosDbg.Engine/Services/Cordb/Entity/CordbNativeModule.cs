namespace ChaosDbg.Cordb
{
    public class CordbNativeModule : ICordbModule
    {
        //todo: set these
        public string Name { get; }
        public long BaseAddress { get; }
        public int Size { get; }
        public long EndAddress => BaseAddress + Size;

        public CordbManagedModule ManagedModule { get; set; }

        public CordbNativeModule(long baseAddress, string name, int size)
        {
            BaseAddress = baseAddress;
            Name = name;
            Size = size;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
