using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Cordb
{
    [DebuggerTypeProxy(typeof(CordbEventHistoryStoreDebugView))]
    class CordbEventHistoryStore : IEnumerable<ICordbEventHistoryItem>
    {
        private object objLock = new object();

        private List<ICordbEventHistoryItem> history = new List<ICordbEventHistoryItem>();

        public int Count
        {
            get
            {
                lock (objLock)
                    return history.Count;
            }
        }

        public int NativeEventCount { get; private set; }

        public int ManagedEventCount { get; private set; }

        public void Add(CordbNativeEventHistoryItem item)
        {
            lock (objLock)
            {
                NativeEventCount++;

                history.Add(item);
            }
        }

        public void Add(CordbManagedEventHistoryItem item)
        {
            lock (objLock)
            {
                ManagedEventCount++;

                history.Add(item);
            }
        }

        public IEnumerator<ICordbEventHistoryItem> GetEnumerator()
        {
            lock (objLock)
            {
                return history.ToArray().Cast<ICordbEventHistoryItem>().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
