using System;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed module that is backed by a <see cref="CorDebugModule"/>.
    /// </summary>
    public class CordbManagedModule : CordbModule
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

                    var modules = sos.GetAssemblyModuleList(Assembly!.ClrAddress);

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

        public new CORDB_ADDRESS BaseAddress => base.BaseAddress;

        public new CORDB_ADDRESS EndAddress => base.EndAddress;

        /// <summary>
        /// Gets the native counterpart of this managed module. This value is only set when interop debugging.
        /// </summary>
        public CordbNativeModule? NativeModule { get; set; }

        /// <summary>
        /// Gets the raw <see cref="CorDebugModule"/> that underpins this object.
        /// </summary>
        public CorDebugModule CorDebugModule { get; }

        private MetaDataProvider? metaDataProvider;

        /// <summary>
        /// Gets a <see cref="MetaDataProvider"/> that provides access to the metadata contained in this module.
        /// </summary>
        public MetaDataProvider MetaDataProvider
        {
            get
            {
                if (metaDataProvider == null)
                    metaDataProvider = new MetaDataProvider(CorDebugModule.GetMetaDataInterface<MetaDataImport>());

                return metaDataProvider;
            }
        }

        public bool IsDynamic => CorDebugModule.IsDynamic;

        public CordbManagedModule(CorDebugModule corDebugModule, CordbProcess process, PEFile? peFile) : base(corDebugModule.Name, corDebugModule.BaseAddress, corDebugModule.Size, process, peFile)
        {
            CorDebugModule = corDebugModule;
        }
    }
}
