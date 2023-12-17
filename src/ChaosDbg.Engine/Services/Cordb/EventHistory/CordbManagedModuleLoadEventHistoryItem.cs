using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbManagedModuleLoadEventHistoryItem : CordbManagedEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Load {Name}";

        public string Name { get; }

        public CordbManagedModuleLoadEventHistoryItem(LoadModuleCorDebugManagedCallbackEventArgs e) : base(e.Kind)
        {
            Name = e.Module.Name;
        }
    }
}
