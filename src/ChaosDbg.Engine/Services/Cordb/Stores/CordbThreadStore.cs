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

    [DebuggerDisplay("Count = {Threads.Count}")]
    [DebuggerTypeProxy(typeof(CordbThreadStoreDebugView))]
    public class CordbThreadStore : IDbgThreadStoreInternal, IEnumerable<CordbThread>, IDisposable
    {
        private object threadLock = new object();

        [Obsolete]
        private Dictionary<int, CordbThread> threads = new Dictionary<int, CordbThread>();

        private Dictionary<int, CordbThread> Threads
        {
            get
            {
                if (!process.IsV3)
                    return threads;

                //In V3, the process is always live. Any time we ask for the threads, synthesize a new dictionary
                //from the current threads of the process.
                var rawThreads = process.CorDebugProcess.Threads;

                var dict = new Dictionary<int, CordbThread>();

                for (var i = 0; i < rawThreads.Length; i++)
                {
                    dict[rawThreads[i].Id] = new CordbThread(i, new CordbThread.ManagedAccessor(rawThreads[i]), process);
                }

                return dict;
            }
        }

        private CordbProcess process;
        private int nextUserId;
        private bool disposed;

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
                    var managed = Threads.Values.FirstOrDefault(t => t.IsManaged && t.SpecialType == null);

                    //Prefer any managed threads we have?
                    if (managed != null)
                        return managed;

                    var native = Threads.Values.FirstOrDefault(t => !t.IsManaged && t.SpecialType == null);

                    //Prefer any normal native threads?
                    if (native != null)
                        return native;

                    //Return any thread we have then, special or not
                    return Threads.Values.FirstOrDefault();
                }
            }
            set => explicitActiveThread = value;
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
                //In V3 mode, we might open a thread handle in the ManagedAccessor upon creating the object. So if we throw
                //prior to creating the object, we have nothing to dispose
                if (disposed)
                    throw new ObjectDisposedException(nameof(CordbThreadStore));

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
                Threads.Add(corDebugThread.Id, thread);
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

            //Everything needs to be inside the lock, because we lock when we dispose so we need to make sure there's no race between adding and disposing,
            //else we might add a thread that won't get disposed
            lock (threadLock)
            {
                if (disposed)
                {
                    //CREATE_THREAD_DEBUG_EVENT thread handles are not owned by us, they're owned by Kernel32.
                    //It's not our responsibility to close the thread handle
                    throw new ObjectDisposedException(nameof(CordbThreadStore));
                }

                var userId = nextUserId;
                nextUserId++;

                var thread = new CordbThread(userId, new CordbThread.NativeAccessor(id, hThread), process);

                Threads.Add(id, thread);

                return thread;
            }
        }

        internal void SetActiveThread(CorDebugThread corDebugThread)
        {
            if (corDebugThread == null)
                return;

            explicitActiveThread = this[corDebugThread.Id];
        }

        internal void SetActiveThread(int id)
        {
            var thread = this[id];

            explicitActiveThread = thread;
        }

        internal CordbThread this[int id]
        {
            get
            {
                lock (threadLock)
                {
                    CheckIfDisposed();
                    return Threads[id];
                }
            }
        }

        internal CordbThread Remove(int id)
        {
            lock (threadLock)
            {
                if (Threads.TryGetValue(id, out var thread))
                {
                    /* Ideally we would like to assert whether our VolatileOSThreadID still matches the thread's
                     * unique ID at this point. Unfortunately, mscordbi will block you from accessing the VolatileOSThreadID
                     * from the Win32 event thread, preventing us from doing this check. See the comments on
                     * ManagedAccessor.VolatileOSThreadID for more information */

                    Threads.Remove(thread.Id);

                    thread.Dispose();

                    thread.Exited = true;

                    //If this was our active thread, clear it so that next time we're asked what our ActiveThread is, we pick an implicit one
                    if (explicitActiveThread?.Id == id)
                        explicitActiveThread = null;
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
                        localThreads = Threads.Values.ToArray();
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
                    //When we get a debug event for each thread while attaching, we pass that thread to IdentifySpecialThreads(). When we're finished attaching,
                    //we specify null, indicating that we now have all our threads (the managed events of which I believe were sent to us in a random order) and now need to choose the best one
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
            if (localThreads.Length == 0)
                return null;

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
            //or a background thread created by the native process. Either way, because we're in managed code now, we'll go for a thread that is executing
            //managed code

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

            //If we're a native application, the main thread may simply say that it's in a Transition Frame, while a secondary thread that it started says that it's a nativeExeCandidate.
            //This isn't correct. So at this stage, if we have any managed candidates, take the first managed candidate

            if (managedCandidates.Length > 0)
                return managedCandidates[0];

            if (nativeExeCandidates.Count > 0)
                throw new NotImplementedException($"Don't know how to choose the best native entry point from {nativeExeCandidates.Count} potential candidates");

            throw new NotImplementedException("Couldn't figure out how to identify the main thread");
        }

        public bool TryGetThreadByUserId(int userId, out CordbThread thread)
        {
            lock (threadLock)
            {
                foreach (var candidate in Threads.Values)
                {
                    if (candidate.UserId == userId)
                    {
                        thread = candidate;
                        return true;
                    }
                }
            }

            thread = default;
            return false;
        }

        internal void SaveRegisterContexts()
        {
            lock (threadLock)
            {
                foreach (var thread in Threads.Values)
                    thread.TrySaveRegisterContext(true);
            }
        }

        #region IDbgThreadStore

        IDbgThread IDbgThreadStoreInternal.ActiveThread => ActiveThread;

        #endregion

        public IEnumerator<CordbThread> GetEnumerator()
        {
            lock (threadLock)
            {
                return Threads.Values.ToArray().AsEnumerable().GetEnumerator();
            }
        }

        IEnumerator<IDbgThread> IDbgThreadStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void CheckIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CordbThreadStore));
        }

        public void Dispose()
        {
            //Native objects have nothing to dispose (their handles are owned by Kernel32)
            //but in V3 our ManagedAccessors may have opened a thread handle

            lock (threadLock)
            {
                if (disposed)
                    return;

                foreach (var thread in threads.Values)
                {
                    thread.Dispose();
                }

                Threads.Clear();

                disposed = true;
            }
        }
    }
}
