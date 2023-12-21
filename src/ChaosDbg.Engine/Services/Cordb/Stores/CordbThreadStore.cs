using System;
using System.Collections;
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
            //We don't need to worry about attach; we'll receive fake create thread messages for both
            //native and managed threads

            this.process = process;
        }

        /// <summary>
        /// Adds a new thread that is attempting to execute managed code.<para/>
        /// This method should only be called when not interop debugging.
        /// </summary>
        /// <param name="corDebugThread">The <see cref="CorDebugThread"/> that represents the CLR-aware thread.</param>
        /// <returns>A <see cref="CordbThread"/> that encapsulates the specified <see cref="CorDebugThread"/>.</returns>
        internal CordbThread Add(CorDebugThread corDebugThread)
        {
            lock (threadLock)
            {
                /* In "hosted" scenarios (I suppose such as SQL Server) a managed thread may run on multiple
                 * native threads. Another possible scenario is that we're running on fibers instead of threads,
                 * however the debugger explicitly prohibits creating a CorDebugProcess when fibers are in use
                 * (you'll get CORDBG_E_CANNOT_DEBUG_FIBER_PROCESS). I don't know what the bookkeeping will
                 * look like when a managed thread actually gets moved to a different native thread, so
                 * we'll just try and catch that that might be happening in Remove(). */

                var thread = new CordbThread(new CordbThread.ManagedAccessor(corDebugThread), process);

                /* When we remove the thread, we're going to assert that the VolatileOSThreadID matches the "unique ID".
                /* That way, if we ever see a scenario where a managed thread got moved to a different physical thread,
                 * we can investigate what notifications we may be able to receive to update our respective bookkeeping.
                 * Reducing the likelihood of this issue even further is the fact that Add() will only be called for managed
                 * threads outside of interop debugging when a CordbProcess is created where a bunch of threads already exist,
                 * which we'll probably only see in attach scenarios (.NET Core CreateProcess scenarios don't seen to apply,
                 * as we do get a bunch of native/managed CreateThread events shortly after attaching to the newly created process). */
                threads.Add(corDebugThread.Id, thread);

                return thread;
            }
        }

        /// <summary>
        /// Adds a new thread that so far is only executing unmanaged code.<para/>
        /// If the thread begins executing managed code, its <see cref="CordbThread.Accessor"/> will be upgraded to a
        /// <see cref="CordbThread.ManagedAccessor"/>.
        /// </summary>
        /// <param name="id">The OS Thread ID of the newly created thread.</param>
        /// <param name="hThread">A handle to the thread.</param>
        /// <returns>A new <see cref="CordbThread"/>.</returns>
        internal CordbThread Add(int id, IntPtr hThread)
        {
            Debug.Assert(process.Session.IsInterop);

            lock (threadLock)
            {
                var thread = new CordbThread(new CordbThread.NativeAccessor(id, hThread), process);

                threads.Add(id, thread);

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
                {
                    /* Ideally we would like to assert whether our VolatileOSThreadID still matches the thread's
                     * unique ID at this point. Unfortunately, mscordbi will block you from accessing the VolatileOSThreadID
                     * from the Win32 event thread, preventing us from doing this check. See the comments on
                     * ManagedAccessor.VolatileOSThreadID for more information */

                    threads.Remove(thread.Id);

                    thread.Exited = true;
                }

                return thread;
            }
        }

        public IEnumerator<CordbThread> GetEnumerator()
        {
            lock (threadLock)
            {
                return threads.Values.ToArray().AsEnumerable().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
