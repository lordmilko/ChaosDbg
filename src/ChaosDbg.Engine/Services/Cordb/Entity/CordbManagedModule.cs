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

        public CordbManagedModule(CorDebugModule corDebugModule, IPEFile peFile) : base(corDebugModule.Name, corDebugModule.BaseAddress, corDebugModule.Size, peFile)
        {
            CorDebugModule = corDebugModule;
        }

        public override string ToString() => CorDebugModule.ToString();
    }
}
