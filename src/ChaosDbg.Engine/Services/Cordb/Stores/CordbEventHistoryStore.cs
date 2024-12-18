﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Cordb
{
    [DebuggerTypeProxy(typeof(CordbEventHistoryStoreDebugView))]
    class CordbEventHistoryStore : IEnumerable<CordbEventHistoryItem>
    {
        private object objLock = new object();

        //Contains a list of all "important events" that have occurred during the life of the debugger,
        //be they managed/native callback occurrences, or reasons that the debugger event loop was told to stop.
        private List<CordbEventHistoryItem> history = new List<CordbEventHistoryItem>();

        //Contains a list of all reasons that the debugger was ever told to stop. These events are also contained
        //in the main "history" list.
        private List<CordbPauseReason> stopReasons = new List<CordbPauseReason>();

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

        //Protects against races between us adding a stop reason and a callback
        //querying what the last stop reason was. e.g. if a callback is asking what
        //the last stop reason as right after we asked for a stop, it's going to think
        //that no stop reason was provided since it already has the latest stop reason
        internal readonly object StopReasonLock = new object();

        public CordbPauseReason LastStopReason
        {
            get
            {
                lock (objLock)
                {
                    if (stopReasons.Count == 0)
                        return null;

                    return stopReasons.Last();
                }
            }
        }

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

        public void Add(CordbPauseReason stopReason)
        {
            lock (objLock)
            lock (StopReasonLock)
            {
                history.Add(stopReason);
                stopReasons.Add(stopReason);
            }
        }

        public void Add(CordbEngineFailureEventHistoryItem item)
        {
            lock (objLock)
                history.Add(item);
        }

        public IEnumerator<CordbEventHistoryItem> GetEnumerator()
        {
            lock (objLock)
            {
                return ((IEnumerable<CordbEventHistoryItem>) history.ToArray()).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
