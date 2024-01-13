using ChaosLib.Metadata;

namespace ChaosDbg.Cordb
{
    public abstract class CordbModule : IDbgModule
    {
        public string Name { get; }

        public long BaseAddress { get; }

        public int Size { get; }

        public long EndAddress { get; }

        public CordbProcess Process { get; }

        public IPEFile PEFile { get; }

        public bool IsExe => !PEFile.FileHeader.Characteristics.HasFlag(ImageFile.Dll);

        protected CordbModule(string name, long baseAddress, int size, CordbProcess process, IPEFile peFile)
        {
            Name = name;
            BaseAddress = baseAddress;
            Size = size;
            EndAddress = baseAddress + size;
            Process = process;
            PEFile = peFile;
        }
    }
}
