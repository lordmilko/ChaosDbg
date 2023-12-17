using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbNativeModuleLoadEventHistoryItem : CordbNativeEventHistoryItem
    {
        protected override string DebuggerDisplay => $"Load {Module.Name}";

        public CordbNativeModule Module { get; }

        public CordbNativeModuleLoadEventHistoryItem(CordbNativeModule module) : base(DebugEventType.LOAD_DLL_DEBUG_EVENT)
        {
            Module = module;
        }
    }
}
