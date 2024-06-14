using System;
using System.Collections;
using System.Collections.Generic;

namespace ChaosDbg.DbgEng
{
    public class DbgEngThreadStore : IDbgThreadStoreInternal, IEnumerable<DbgEngThread>
    {
        private object threadLock = new object();

        private Dictionary<int, DbgEngThread> threads = new Dictionary<int, DbgEngThread>();
        
        public DbgEngThread ActiveThread { get; set; }

        private DbgEngProcess process;

        public DbgEngThreadStore(DbgEngProcess process)
        {
            this.process = process;
        }

        internal DbgEngThread Add(int userId, int systemId)
        {
            var thread = new DbgEngThread(userId, systemId);
            
            //If somehow we already have a thread with the specified system ID, this may
            //potentially indicate a bug and we'd like this to explode
            lock (threadLock)
                threads.Add(systemId, thread);
            
            return thread;
        }

        internal DbgEngThread Remove(int systemId)
        {
            lock (threadLock)
            {
                if (threads.TryGetValue(systemId, out var thread))
                    threads.Remove(thread.SystemId);

                return thread;
            }
        }

        #region IDbgThreadStoreInternal

        IDbgThread IDbgThreadStoreInternal.ActiveThread => ActiveThread;

        #endregion

        public IEnumerator<DbgEngThread> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator<IDbgThread> IDbgThreadStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
