using System;
using ChaosLib.PortableExecutable;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents either a native or managed module that has been loaded into the current process.
    /// </summary>
    public abstract class CordbModule : IDbgModule
    {
        long IDbgModule.BaseAddress => BaseAddress;
        long IDbgModule.EndAddress => EndAddress;

        public string Name { get; }

        public CORDB_ADDRESS BaseAddress { get; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress { get; }

        public CordbAssembly? Assembly { get; internal set; }

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> associated with this module.
        /// </summary>
        public CordbProcess Process { get; }

        protected readonly IPEFile? peFile;
        private bool hasFullPEFile;

        /// <inheritdoc />
        public IPEFile? PEFile
        {
            get
            {
                if (!hasFullPEFile && peFile != null)
                {
                    Process.Symbols.EnsureModuleLoaded(BaseAddress, false);
                    hasFullPEFile = true;
                }

                return peFile;
            }
        }

        public bool IsExe => peFile != null && !peFile.FileHeader.Characteristics.HasFlag(ImageFile.Dll);

        protected CordbModule(string name, long baseAddress, int size, CordbProcess process, IPEFile? peFile)
        {
            Name = name;
            BaseAddress = baseAddress;
            Size = size;
            EndAddress = baseAddress + size;
            Process = process;

            //We can have no PEFile when we have a dynamic module
            this.peFile = peFile;
            hasFullPEFile = peFile == null;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
