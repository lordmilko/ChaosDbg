using System;
using ChaosLib.PortableExecutable;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents either a native or managed module that has been loaded into the current process.
    /// </summary>
    public abstract class CordbModule : IDbgModule
    {
        #region ClrAddress

        private CLRDATA_ADDRESS? clrAddress;

        /// <summary>
        /// Gets the address of the clr!Module that this object represents.
        /// </summary>
        public CLRDATA_ADDRESS ClrAddress
        {
            get
            {
                if (clrAddress == null)
                {
                    var sos = Process.DAC.SOS;

                    var modules = sos.GetAssemblyModuleList(Assembly.ClrAddress);

                    //Return 0 for now
                    if (modules.Length == 0)
                        return 0;

                    if (modules.Length > 1)
                        throw new NotImplementedException($"Handling assemblies with {modules.Length} modules is not implemented.");

                    clrAddress = modules[0];
                }

                return clrAddress.Value;
            }
        }

        #endregion

        long IDbgModule.BaseAddress => BaseAddress;
        long IDbgModule.EndAddress => EndAddress;

        public string Name { get; }

        public CORDB_ADDRESS BaseAddress { get; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress { get; }

        public CordbAssembly Assembly { get; internal set; }

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

        public override string ToString()
        {
            return Name;
        }
    }
}
