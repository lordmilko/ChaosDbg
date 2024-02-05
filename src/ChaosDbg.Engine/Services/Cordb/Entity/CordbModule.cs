using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public abstract class CordbModule : IDbgModule
    {
        long IDbgModule.BaseAddress => BaseAddress;
        long IDbgModule.EndAddress => EndAddress;

        public string Name { get; }

        public CORDB_ADDRESS BaseAddress { get; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress { get; }

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> associated with this module.
        /// </summary>
        public CordbProcess Process { get; }

        /// <inheritdoc />
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
