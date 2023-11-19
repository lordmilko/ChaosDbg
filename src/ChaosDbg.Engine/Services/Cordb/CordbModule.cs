using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbModule : IDbgModule
    {
        long IDbgModule.BaseAddress => BaseAddress;
        long IDbgModule.EndAddress => EndAddress;

        public CORDB_ADDRESS BaseAddress { get; }

        public int Size { get; }

        public CORDB_ADDRESS EndAddress => BaseAddress + Size;

        private readonly CorDebugModule corDebugModule;

        public CordbModule(CorDebugModule corDebugModule)
        {
            this.corDebugModule = corDebugModule;

            BaseAddress = corDebugModule.BaseAddress;
            Size = corDebugModule.Size;
        }

        public override string ToString() => corDebugModule.ToString();
    }
}
