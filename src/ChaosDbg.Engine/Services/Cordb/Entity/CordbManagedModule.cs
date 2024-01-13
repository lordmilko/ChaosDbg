using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbManagedModule : CordbModule
    {
        public new CORDB_ADDRESS BaseAddress => base.BaseAddress;

        public new CORDB_ADDRESS EndAddress => base.EndAddress;

        public CordbNativeModule NativeModule { get; set; }

        public CorDebugModule CorDebugModule { get; }

        public MetaDataProvider MetaDataProvider { get; }

        public bool IsDynamic => CorDebugModule.IsDynamic;

        public CordbManagedModule(CorDebugModule corDebugModule, CordbProcess process, IPEFile peFile) : base(corDebugModule.Name, corDebugModule.BaseAddress, corDebugModule.Size, process, peFile)
        {
            CorDebugModule = corDebugModule;
            MetaDataProvider = new MetaDataProvider(corDebugModule.GetMetaDataInterface<MetaDataImport>());
        }

        public override string ToString() => CorDebugModule.ToString();
    }
}
