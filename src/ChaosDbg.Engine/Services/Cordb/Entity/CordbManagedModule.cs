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

        public CordbManagedModule(CorDebugModule corDebugModule, CordbProcess process, IPEFile peFile) : base(corDebugModule.Name, corDebugModule.BaseAddress, corDebugModule.Size, process, peFile)
        {
            CorDebugModule = corDebugModule;
        }
    }
}
