﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.DbgEng
{
    public class DbgEngThreadStore : IDbgThreadStoreInternal, IEnumerable<DbgEngThread>
    {
        private object threadLock = new object();

        private Dictionary<int, DbgEngThread> threads = new Dictionary<int, DbgEngThread>();
        
        public DbgEngThread ActiveThread { get; set; }

        private readonly DbgEngProcess process;

        public DbgEngThreadStore(DbgEngProcess process)
        {
            this.process = process;
        }

        internal DbgEngThread Add(int userId, int systemId, long handle, long tebAddress)
        {
            //Everything needs to be inside the lock, because we lock when we dispose so we need to make sure there's no race between adding and disposing,
            //else we might add a thread that won't get disposed
            lock (threadLock)
            {
                var thread = new DbgEngThread(userId, systemId, handle, tebAddress, process);

                //If somehow we already have a thread with the specified system ID, this may
                //potentially indicate a bug and we'd like this to explode
                threads.Add(systemId, thread);

                return thread;
            }
        }

        internal DbgEngThread Remove(int systemId)
        {
            lock (threadLock)
            {
                if (threads.TryGetValue(systemId, out var thread))
                {
                    threads.Remove(thread.SystemId);
                }

                return thread;
            }
        }

        #region IDbgThreadStoreInternal

        IDbgThread IDbgThreadStoreInternal.ActiveThread => ActiveThread;

        #endregion

        public IEnumerator<DbgEngThread> GetEnumerator()
        {
            lock (threadLock)
                return ((IEnumerable<DbgEngThread>) threads.Values.ToArray()).GetEnumerator();
        }

        IEnumerator<IDbgThread> IDbgThreadStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
