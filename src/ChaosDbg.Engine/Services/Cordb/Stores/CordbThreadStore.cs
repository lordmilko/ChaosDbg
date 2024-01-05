using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosLib.Metadata;
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
        private int nextUserId;

        //Stores the active thread the user has explicitly specified, if applicable.
        private CordbThread explicitActiveThread;

        /// <summary>
        /// Gets or sets thread that user interactions with the debugger should apply to.
        /// </summary>
        public CordbThread ActiveThread
        {
            get
            {
                //If the user has set their active thread, prefer that
                if (explicitActiveThread != null)
                    return explicitActiveThread;

                //If we know the main thread, use that
                if (MainThread != null)
                    return MainThread;

                lock (threadLock)
                {
                    var managed = threads.Values.FirstOrDefault(t => t.IsManaged && t.SpecialType == null);

                    //Prefer any managed threads we have?
                    if (managed != null)
                        return managed;

                    var native = threads.Values.FirstOrDefault(t => !t.IsManaged && t.SpecialType == null);

                    //Prefer any normal native threads?
                    if (native != null)
                        return native;

                    //Return any thread we have then, special or not
                    return threads.Values.FirstOrDefault();
                }
            }
        }

        public CordbThread MainThread { get; private set; }

        public CordbThread FinalizerThread { get; private set; }

        public CordbThread GCThread { get; private set; }

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
            CordbThread thread;

            lock (threadLock)
            {
                /* In "hosted" scenarios (I suppose such as SQL Server) a managed thread may run on multiple
                 * native threads. Another possible scenario is that we're running on fibers instead of threads,
                 * however the debugger explicitly prohibits creating a CorDebugProcess when fibers are in use
                 * (you'll get CORDBG_E_CANNOT_DEBUG_FIBER_PROCESS). I don't know what the bookkeeping will
                 * look like when a managed thread actually gets moved to a different native thread, so
                 * we'll just try and catch that that might be happening in Remove(). */

                var userId = nextUserId;
                nextUserId++;

                thread = new CordbThread(userId, new CordbThread.ManagedAccessor(corDebugThread), process);

                /* When we remove the thread, we're going to assert that the VolatileOSThreadID matches the "unique ID".
                /* That way, if we ever see a scenario where a managed thread got moved to a different physical thread,
                 * we can investigate what notifications we may be able to receive to update our respective bookkeeping.
                 * Reducing the likelihood of this issue even further is the fact that Add() will only be called for managed
                 * threads outside of interop debugging when a CordbProcess is created where a bunch of threads already exist,
                 * which we'll probably only see in attach scenarios (.NET Core CreateProcess scenarios don't seen to apply,
                 * as we do get a bunch of native/managed CreateThread events shortly after attaching to the newly created process). */
                threads.Add(corDebugThread.Id, thread);
            }

            //Must be outside of the lock
            IdentifySpecialThreads(thread);

            return thread;
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
                var userId = nextUserId;
                nextUserId++;

                var thread = new CordbThread(userId, new CordbThread.NativeAccessor(id, hThread), process);

                threads.Add(id, thread);

                return thread;
            }
        }

        internal void SetActiveThread(CorDebugThread corDebugThread)
        {
            if (corDebugThread == null)
                return;

            explicitActiveThread = this[corDebugThread.Id];
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

        public void IdentifySpecialThreads(CordbThread thread)
        {
            //This method inherently should only ever be called with managed threads, or null
            Debug.Assert(thread == null || thread.IsManaged);

            CordbThread[] localThreads = null;

            void EnsureThreads()
            {
                if (localThreads == null)
                {
                    lock (threadLock)
                        localThreads = threads.Values.ToArray();
                }
            }

            if (FinalizerThread == null)
            {
                EnsureThreads();

                FinalizerThread = localThreads.SingleOrDefault(t => t.IsFinalizer);
            }

            if (GCThread == null)
            {
                EnsureThreads();

                GCThread = localThreads.SingleOrDefault(t => t.IsGC);
            }

            if (MainThread == null)
            {
                if (process.Session.IsAttaching)
                {
                    if (thread != null)
                        return;

                    //We're receiving the attach complete message

                    EnsureThreads();

                    MainThread = CalculateMainThread(localThreads);
                }
                else
                {
                    EnsureThreads();

                    if (thread.IsManaged && thread.SpecialType == null)
                    {
                        thread.IsMain = true;
                        MainThread = thread;
                    }
                }
            }
        }

        private CordbThread CalculateMainThread(CordbThread[] localThreads)
        {
            if (process.Session.IsInterop)
            {
                /* When interop debugging, DbgkpPostFakeThreadMessages does seem to notify us of thread creation
                 * events in the same order they were created. This contrasts with ICorDebug, which notifies us
                 * of our threads on attach in an arbitrary order. We can't just go for the thread with UserId 0,
                 * because if this is a purely managed process, I imagine a special CLR thread could potentially
                 * start before our normal main thread does */
                return localThreads.OrderBy(t => t.UserId).First(t => t.SpecialType == null);
            }

            //Managed

            //This may be a managed process, or a native process that spun up the CLR. And the CLR may have been spun up on the actual main thread,
            //or a background thread created by the native process. Either way, because we're 

            var managedCandidates = localThreads.Where(v => v.IsManaged && v.SpecialType == null).ToArray();

            if (managedCandidates.Length == 1)
                return managedCandidates[0];

            var managedExeCandidates = new List<Tuple<CordbThread, CordbILFrame, int>>();
            var nativeExeCandidates = new List<CordbThread>();

            foreach (var managedCandidate in managedCandidates)
            {
                var stackTrace = managedCandidate.StackTrace.Reverse().ToArray();

                for (var i = 0; i < stackTrace.Length; i++)
                {
                    var frame = stackTrace[i];

                    //Because we're not interop, we won't be able to see any native frames

                    if (frame is CordbILFrame f && f.Module != null && f.Module.IsExe)
                    {
                        var imageCor20Header = frame.Module.PEFile.Cor20Header;

                        if (imageCor20Header != null && imageCor20Header.Flags.HasFlag(COMIMAGE_FLAGS.ILONLY))
                            managedExeCandidates.Add(Tuple.Create(managedCandidate, f, i));
                        else
                            nativeExeCandidates.Add(managedCandidate);

                        break;
                    }
                }
            }

            if (managedExeCandidates.Count > 0)
            {
                if (managedExeCandidates.Count == 1)
                    return managedExeCandidates[0].Item1;

                var entryPointMatches = new List<CordbThread>();

                foreach (var item in managedExeCandidates)
                {
                    var token = (mdToken) item.Item2.Module.PEFile.Cor20Header.EntryPointTokenOrRelativeVirtualAddress;

                    switch (token.Type)
                    {
                        case CorTokenType.mdtMethodDef:
                            if (item.Item2.CorDebugFrame.FunctionToken == (mdMethodDef) token)
                                entryPointMatches.Add(item.Item1);

                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle entry point token of type {token.Type}.");
                    }
                }

                if (entryPointMatches.Count == 1)
                    return entryPointMatches[0];

                return (entryPointMatches.Count > 1 ? entryPointMatches : managedExeCandidates.Select(v => v.Item1))
                    .Select(v => new {Id = v.DacThreadData?.corThreadId, Thread = v})
                    .Where(v => v.Id != null)
                    .OrderBy(v => v.Id)
                    .First().Thread;
            }

            if (nativeExeCandidates.Count > 0)
                throw new NotImplementedException($"Don't know how to choose the best native entry point from {nativeExeCandidates.Count} potential candidates");

            throw new NotImplementedException("Couldn't figure out how to identify the main thread");
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
