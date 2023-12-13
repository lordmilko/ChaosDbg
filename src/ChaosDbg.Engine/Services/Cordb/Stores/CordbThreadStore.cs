﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbThreadStoreDebugView
    {
        private CordbThreadStore store;

        public CordbThreadStoreDebugView(CordbThreadStore store)
        {
            this.store = store;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public CordbThread[] Items => store.ToArray();
    }

    [DebuggerDisplay("Count = {threads.Count}")]
    [DebuggerTypeProxy(typeof(CordbThreadStoreDebugView))]
    public class CordbThreadStore : IEnumerable<CordbThread>
    {
        private object threadLock = new object();

        private Dictionary<int, CordbThread> threads = new Dictionary<int, CordbThread>();
        private CordbProcess process;

        /// <summary>
        /// Gets or sets thread that user interactions with the debugger should apply to.
        /// </summary>
        public CordbThread ActiveThread { get; set; }

        public CordbThreadStore(CordbProcess process)
        {
            this.process = process;

            foreach (var thread in process.CorDebugProcess.Threads)
                Add(thread);
        }

        internal CordbThread Add(CorDebugThread corDebugThread)
        {
            lock (threadLock)
            {
                var thread = new CordbThread(corDebugThread, process);

                threads.Add(corDebugThread.Id, thread);

                return thread;
            }
        }

        internal void SetActiveThread(CorDebugThread corDebugThread)
        {
            if (corDebugThread == null)
                return;

            ActiveThread = this[corDebugThread.Id];
        }

        internal CordbThread this[int id]
        {
            get
            {
                lock (threadLock)
                    return threads[id];
            }
        }

        internal CordbThread Remove(int id)
        {
            lock (threadLock)
            {
                if (threads.TryGetValue(id, out var thread))
                    threads.Remove(thread.Id);

                return thread;
            }
        }

        public IEnumerator<CordbThread> GetEnumerator()
        {
            lock (threadLock)
            {
                return threads.Values.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
