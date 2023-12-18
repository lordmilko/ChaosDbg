using System;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbEventHistoryStoreDebugView
    {
        public ICordbEventHistoryItem[] AllEvents => store.ToArray();

        public ICordbThreadEventHistoryItem[] ThreadEvents => GetEvents<ICordbThreadEventHistoryItem>(
            DebugEventType.CREATE_THREAD_DEBUG_EVENT,
            DebugEventType.EXIT_THREAD_DEBUG_EVENT,
            CorDebugManagedCallbackKind.CreateThread,
            CorDebugManagedCallbackKind.ExitThread
        );

        public ICordbEventHistoryItem[] ModuleEvents => GetEvents<ICordbEventHistoryItem>(
            DebugEventType.LOAD_DLL_DEBUG_EVENT,
            DebugEventType.UNLOAD_DLL_DEBUG_EVENT,
            CorDebugManagedCallbackKind.LoadModule,
            CorDebugManagedCallbackKind.UnloadModule
        );

        private CordbEventHistoryStore store;

        public CordbEventHistoryStoreDebugView(CordbEventHistoryStore store)
        {
            this.store = store;
        }

        private T[] GetEvents<T>(params object[] kinds) where T : ICordbEventHistoryItem
        {
            var results = new List<T>();

            var nativeKinds = kinds.OfType<DebugEventType>().ToArray();
            var managedKinds = kinds.OfType<CorDebugManagedCallbackKind>().ToArray();

            foreach (var item in store.OfType<T>())
            {
                if (item is CordbNativeEventHistoryItem n)
                {
                    if (nativeKinds.Contains(n.NativeEventKind))
                        results.Add(item);
                }
                else if (item is CordbManagedEventHistoryItem m)
                {
                    if (managedKinds.Contains(m.ManagedEventKind))
                        results.Add(item);
                }
                else
                {
                    throw new NotImplementedException($"Don't know how to handle item of type {item.GetType().Name}");
                }
            }

            return results.ToArray();
        }
    }
}
