using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbNativeModuleUnloadEventHistoryItem : CordbNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Unload {Module.Name}";

        public CordbNativeModule Module { get; }

        public CordbNativeModuleUnloadEventHistoryItem(CordbNativeModule module) : base(DebugEventType.UNLOAD_DLL_DEBUG_EVENT)
        {
            Module = module;
        }
    }
}
