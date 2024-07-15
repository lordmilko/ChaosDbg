using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.DbgEng
{
    class DbgEngEventHistoryStore : IEnumerable<DbgEngEventHistoryItem>
    {
        private object objLock = new object();

        //Contains a list of all "important events" that have occurred during the life of the debugger,
        //be they managed/native callback occurrences, or reasons that the debugger event loop was told to stop.
        private List<DbgEngEventHistoryItem> history = new List<DbgEngEventHistoryItem>();

        //Contains a list of all reasons that the debugger was ever told to stop. These events are also contained
        //in the main "history" list.
        private List<DbgEngPauseReason> stopReasons = new List<DbgEngPauseReason>();

        public int Count
        {
            get
            {
                lock (objLock)
                    return history.Count;
            }
        }

        public DbgEngPauseReason LastStopReason
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

        public void Add(DbgEngEventHistoryItem item)
        {
            lock (objLock)
            {
                history.Add(item);
            }
        }

        public void Add(DbgEngPauseReason stopReason)
        {
            lock (objLock)
            {
                history.Add(stopReason);
                stopReasons.Add(stopReason);
            }
        }

        public void Add(DbgEngEngineFailureEventHistoryItem item)
        {
            lock (objLock)
                history.Add(item);
        }

        public IEnumerator<DbgEngEventHistoryItem> GetEnumerator()
        {
            lock (objLock)
            {
                return ((IEnumerable<DbgEngEventHistoryItem>) history.ToArray()).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
