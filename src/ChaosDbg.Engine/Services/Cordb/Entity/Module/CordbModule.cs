using System.IO;
using ClrDebug;
using PESpy;
using SymHelp.Symbols;

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

        public string FullName { get; }

        public CORDB_ADDRESS BaseAddress { get; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress { get; }

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> associated with this module.
        /// </summary>
        public CordbProcess Process { get; }

        /// <inheritdoc />
        public PEFile? PEFile { get; }

        public bool IsExe => PEFile != null && !PEFile.FileHeader.Characteristics.HasFlag(ImageFile.Dll);

        /// <summary>
        /// Gets or sets whether this module is currently loaded in the target process.<para/>
        /// This value is set to <see langword="false"/> when the debugger receives the event that the module has unloaded. Note that the debugger is notified
        /// that a module is unloaded after it has already been removed from the memory of the target process.<para/>As such, this value may still be <see langword="true"/>
        /// if the debugger has yet to receive a module's unload event.
        /// </summary>
        public bool IsLoaded { get; internal set; } = true;

        private ISymbolModule? symbolModule;

        /// <summary>
        /// Provides access to the symbol information that is available for this module.<para/>
        /// If no symbols are available, a synthetic symbol module will be returned that will provide access to any information that can be calculated at runtime.
        /// </summary>
        public ISymbolModule SymbolModule
        {
            get
            {
                if (symbolModule == null)
                    symbolModule = Process.Symbols.GetSymbolModule(BaseAddress);

                return symbolModule;
            }
        }

        protected CordbModule(string name, long baseAddress, int size, CordbProcess process, PEFile? peFile)
        {
            Name = Path.IsPathRooted(name) ? Path.GetFileNameWithoutExtension(name) : name;
            FullName = name;
            BaseAddress = baseAddress;
            Size = size;
            EndAddress = baseAddress + size;
            Process = process;

            //We can have no PEFile when we have a dynamic module
            PEFile = peFile;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
