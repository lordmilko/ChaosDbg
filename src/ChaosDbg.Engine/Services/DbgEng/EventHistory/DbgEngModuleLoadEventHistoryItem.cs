using ClrDebug;

namespace ChaosDbg.DbgEng
{
    class DbgEngModuleLoadEventHistoryItem : DbgEngNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Load {Module.ModuleName}";

        public DbgEngModule Module { get; }

        public DbgEngModuleLoadEventHistoryItem(DbgEngModule module) : base(DebugEventType.LOAD_DLL_DEBUG_EVENT, module.Process.Id)
        {
            Module = module;
        }
    }
}
