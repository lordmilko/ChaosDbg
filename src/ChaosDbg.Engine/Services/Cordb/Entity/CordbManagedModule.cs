using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbManagedModule : ICordbModule
    {
        long IDbgModule.BaseAddress => BaseAddress;
        long IDbgModule.EndAddress => EndAddress;

        public CORDB_ADDRESS BaseAddress { get; }

        public CordbNativeModule NativeModule { get; set; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress => BaseAddress + Size;

        private readonly CorDebugModule corDebugModule;

        public CordbManagedModule(CorDebugModule corDebugModule)
        {
            this.corDebugModule = corDebugModule;

            BaseAddress = corDebugModule.BaseAddress;
            Size = corDebugModule.Size;
        }

        public override string ToString() => corDebugModule.ToString();
    }
}
