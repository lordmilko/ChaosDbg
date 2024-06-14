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

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> associated with this module.
        /// </summary>
        public CordbProcess Process { get; }

        protected readonly PEFile? peFile;
        private bool hasFullPEFile;

        /// <inheritdoc />
        public PEFile? PEFile
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

        /// <summary>
        /// Gets or sets whether this module is currently loaded in the target process.<para/>
        /// This value is set to <see langword="false"/> when the debugger receives the event that the module has unloaded. Note that the debugger is notified
        /// that a module is unloaded after it has already been removed from the memory of the target process.<para/>As such, this value may still be <see langword="true"/>
        /// if the debugger has yet to receive a module's unload event.
        /// </summary>
        public bool IsLoaded { get; internal set; } = true;

        protected CordbModule(string name, long baseAddress, int size, CordbProcess process, PEFile? peFile)
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
