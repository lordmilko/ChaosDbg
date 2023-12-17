using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbManagedModuleUnloadEventHistoryItem : CordbManagedEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Unload {Name}";

        public string Name { get; }

        public long BaseAddress { get; }

        public CordbManagedModuleUnloadEventHistoryItem(UnloadModuleCorDebugManagedCallbackEventArgs e) : base(e.Kind)
        {
            Name = e.Module.Name;
            BaseAddress = e.Module.BaseAddress;
        }
    }
}
