using System;
using ClrDebug;
using PESpy;
using SymHelp.Metadata;
using SymHelp.Symbols;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed module that is backed by a <see cref="CorDebugModule"/>.
    /// </summary>
    public class CordbManagedModule : CordbModule, IClrMetadataProvider
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
                        throw new NotImplementedException($"Handling assemblies with {modules.Length} modules is not implemented."); //Maybe find the best module using ISOSDacInterface.GetModuleData?

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

        public CordbAssembly? Assembly { get; internal set; }

        private MetadataModule? metadataModule;

        /// <summary>
        /// Gets a <see cref="ChaosLib.Metadata.MetadataModule"/> that provides access to the metadata contained in this module.
        /// </summary>
        public MetadataModule MetadataModule
        {
            get
            {
                if (metadataModule == null)
                    metadataModule = Process.Modules.MetadataStore.GetOrAddModule(this);

                return metadataModule;
            }
        }

        public bool IsDynamic => CorDebugModule.IsDynamic;

        public CordbManagedModule(CorDebugModule corDebugModule, CordbProcess process, PEFile? peFile) : base(corDebugModule.Name, corDebugModule.BaseAddress, corDebugModule.Size, process, peFile)
        {
            CorDebugModule = corDebugModule;
        }
    }
}
