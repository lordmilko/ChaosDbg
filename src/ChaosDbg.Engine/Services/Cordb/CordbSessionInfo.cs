using System;
using System.Diagnostics;
using System.Threading;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Stores debugger entities used to manage the current <see cref="CordbEngine"/> session.
    /// </summary>
    public class CordbSessionInfo : IDisposable
    {
        public CorDebug CorDebug { get; set; }

        public CordbManagedCallback ManagedCallback { get; set; }

        public CordbUnmanagedCallback UnmanagedCallback { get; set; }

        public CordbProcess Process { get; set; }

        public EngineStatus LastStatus { get; set; }

        /// <summary>
        /// Gets or sets the current status of the process.
        /// </summary>
        public EngineStatus Status
        {
            get
            {
                lock (UserPauseCountLock)
                {
                    if (UserPauseCount == 0)
                        return EngineStatus.Continue;

                    return EngineStatus.Break;
                }
            }
        }

        /// <summary>
        /// Gets whether both managed and native code are being debugged in the process.
        /// </summary>
        public bool IsInterop { get; set; }

        private int currentStopCount;

        /// <summary>
        /// Gets the current number of times a managed callback has been entered or Stop() has been called
        /// without subsequently calling Continue(). Any time this value is not 0, the process is not freely running.
        /// Note that while in the middle of a callback, this value will naturally be greater than 1, and so this value
        /// cannot be used to determine whether the debugger is actually "paused" from a user perspective.
        /// </summary>
        public int CallbackStopCount
        {
            get => currentStopCount;
            set => Interlocked.Exchange(ref currentStopCount, value);
        }

        internal readonly object UserPauseCountLock = new object();

        /// <summary>
        /// Gets the number of times the engine has currently stopped to prompt for user input. This value should typically be either 0 or 1.
        /// </summary>
        public int UserPauseCount { get; set; }

        /// <summary>
        /// Gets the total number of times that Continue() has been called in the current debug session, ever,
        /// whether it be due to a managed callback completing, or Continue() manually being called by the user
        /// in response to a previous Stop()
        /// </summary>
        public int TotalContinueCount { get; set; }

        /// <summary>
        /// Gets the <see cref="CancellationTokenSource"/> that can be used to terminate all running commands
        /// and shutdown the debug engine and wraps the <see cref="CancellationToken"/> that may have been supplied by the user.<para/>
        /// This property should not be exposed outside of the <see cref="CordbSessionInfo"/>. If we want to cancel our session,
        /// we should call <see cref="CordbSessionInfo.Dispose"/>.
        /// </summary>
        private CancellationTokenSource EngineCancellationTokenSource { get; set; }

        /// <summary>
        /// Gets whether the <see cref="EngineCancellationTokenSource"/> has been cancelled and that the engine is shutting down.
        /// </summary>
        public bool IsEngineCancellationRequested => EngineCancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> of the <see cref="EngineCancellationTokenSource"/> that can be used to notify systems
        /// that the debug engine is shutting down.
        /// </summary>
        public CancellationToken EngineCancellationToken => EngineCancellationTokenSource.Token;

        /// <summary>
        /// Gets the managed thread ID of the <see cref="EngineThread"/>.
        /// </summary>
        public int EngineThreadId => EngineThread.ManagedThreadId;

        /// <summary>
        /// Stores a reference to the internal thread that the debugger engine is running on.
        /// </summary>
        internal DispatcherThread EngineThread { get; }

        /// <summary>
        /// Stores temporary values during the lifetime of a managed or unmanaged callback.
        /// </summary>
        internal CordbCallbackContext CallbackContext { get; }

        /// <summary>
        /// Stores temporary state while the debugger is paused.
        /// </summary>
        internal CordbPauseContext PauseContext { get; } = new CordbPauseContext();

        internal CordbEventHistoryStore EventHistory { get; } = new CordbEventHistoryStore();

        public TaskCompletionSource TargetCreated { get; } = new TaskCompletionSource();

        /// <summary>
        /// Gets or sets whether this process was launched directly by the debugger, or whether it was attached to.
        /// </summary>
        public bool DidCreateProcess { get; internal set; }

        /// <summary>
        /// Gets or sets whether the process is in the process of attaching, wherein the debugger will be informed
        /// of all of the key debugger events that occurred prior to the process becoming debugged.
        /// </summary>
        internal bool IsAttaching { get; set; }

        /// <summary>
        /// Specifies that the a critical error has occurred and that the engine is in the process of crashing. If the critical error occurred before the debug target was even fully initialized,
        /// <see cref="StartupFailed"/> will additionally be set.
        /// </summary>
        internal bool IsCrashing { get; private set; }

        /// <summary>
        /// Specifies that the engine is not only crashing, but that it didn't even complete initialization properly, and that <see cref="Process"/> will be <see langword="null"/>.
        /// </summary>
        internal bool StartupFailed { get; set; }

        /// <summary>
        /// Gets or sets whether a call to <see cref="CorDebugProcess.Terminate"/> has been made and that the engine is now waiting
        /// for a debugger callback to notify it that termination has completed.
        /// </summary>
        internal bool IsTerminating { get; set; }

        /// <summary>
        /// Gets or sets whether the process has been terminated. If the process exits unexpectedly, this may be <see langword="true"/> even if <see cref="IsTerminating"/>
        /// is not set.
        /// </summary>
        internal bool IsTerminated { get; set; }

        /// <summary>
        /// Gets the <see cref="CorDebugProcess"/> that is currently is currently in the process of crashing. This property is only set when <see cref="IsCrashing"/> is <see langword="true"/>,
        /// and is required for cases where <see cref="StartupFailed"/> and so we don't have a <see cref="Process"/> to use for continuing unmanaged events.
        /// </summary>
        internal CorDebugProcess CrashingProcess { get; private set; }

        internal void SetCrashingProcess(CorDebugProcess process)
        {
            //We regularly null out our CordbProcess to signify it's been terminated, but in a critical failure where we couldn't even initialize
            //our CordbProcess, we need to have a reference to a CorDebugProcess so that unmanaged events have something to call Continue() on

            Debug.Assert(process != null);

            CrashingProcess = process;
            IsCrashing = true;
        }

        public bool HaveLoaderBreakpoint { get; internal set; }

        public ManualResetEventSlim WaitExitProcess { get; } = new ManualResetEventSlim(false);

        public bool IsCLRLoaded => EventHistory.ManagedEventCount > 0;

        public int EngineId { get; }

        public CordbEngineServices Services { get; }

        private static int cordbEngineCount;
        private Thread criticalFailureThread;
        private object criticalFailureThreadLock = new object();
        private bool disposed;
        private bool disposing;
        private object disposeLock = new object();

        public CordbSessionInfo(CordbEngineServices services, ThreadStart threadProc, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            Services = services;

            EngineId = Interlocked.Increment(ref cordbEngineCount);
            Log.SetProperty("EngineId", EngineId);

            EngineThread = new DispatcherThread($"Cordb Engine Thread {EngineId}", threadProc, enableLog: true);

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            CallbackContext = new CordbCallbackContext(this);
        }

        /// <summary>
        /// Starts the debug session by launching the engine thread.
        /// </summary>
        public void Start() => EngineThread.Start();

        internal void InitializeCriticalFailureThread(ThreadStart action)
        {
            Debug.Assert(disposed == false);

            lock (criticalFailureThreadLock)
            {
                if (criticalFailureThread != null)
                    return;

                criticalFailureThread = new Thread(action);
                Log.CopyContextTo(criticalFailureThread);
                criticalFailureThread.Name = $"Fatal Shutdown Thread {EngineId}";
                criticalFailureThread.Start();
            }
        }

        public void Dispose()
        {
            if (!Monitor.TryEnter(disposeLock))
                return; //Obviously somebody else is already in the process of disposing. e.g. we are the critical failure thread, and the engine thread is waiting for us to end, and the UI thread is waiting for the engine thread to end

            try
            {
                if (disposed)
                    return;

                if (!WaitForCriticalFailureThread())
                    return;

                if (disposing)
                    return;

                disposing = true;
            }
            finally
            {
                Monitor.Exit(disposeLock);
            }

            //The critical failure thread may have already disposed everything
            if (disposed)
                return;

            //First, cancel the CTS if we have one
            EngineCancellationTokenSource?.Cancel();

            //todo: currently not sure about whether the following statement is correct

            //If we're the EngineThread, we need to give up immediately so that we don't accidentally double dispose the UnmanagedCallback,
            //and so that we don't deadlock with us waiting for the critical failure thread to end, and it waiting for us to end.
            //And in any case, if, upon returning from this method, we can see that the critical failure thread cleaned up everything, we're done
            if (!WaitForCriticalFailureThread() || disposed)
                return;

            //Wait for the engine thread to end. If we're going to terminate the target process, we need to do this
            //prior to disposing our callbacks (since after they're disposed, they won't accept new events, which means
            //we'll miss our ExitProcess event)
            EngineThread.Dispose();

            //The unmanaged callback has thread responsible for dispatching
            //in band callbacks that needs to be disposed
            UnmanagedCallback?.Dispose();
            UnmanagedCallback = null;

            ManagedCallback?.Dispose();
            ManagedCallback = null;

            //Every callback/engine thread should be shutdown now, and there should no longer be the possibility for any more critical failures to occur. Wait for the critical failure thread to end (if we have one).
            //If the critical failure thread is trying to dispose the session at the same time that the EngineThread is however, these threads will deadlock each other. In this case, the EngineThread will poll
            //while waiting for the critical failure thread to complete, and if the critical failure thread says its in the middle of disposing, then we'll leave everything else to the critical failure thread to dispose of
            if (!WaitForCriticalFailureThread() || disposed)
                return;

            Process?.Dispose();
            WaitExitProcess.Dispose();

            CrashingProcess = null;

            //Clear out essential debugger objects

            /* We should have already terminated and nullified our ICorDebug upon terminating the process. Complex cleanup work is required
             * to terminate an ICorDebug instance. Furthermore, we want to protect against races wherein we might be calling Cordb::Terminate() from
             * our "normal" locations, and if we then try and do it here at the same time we'll exceptions that various OS handles have already been closed.
             * Thus, we assert that it's not our responsibility to cleanup the ICorDebug instance; it should have been done by the engine */
            Debug.Assert(CorDebug == null, $"Attempted to dispose {nameof(CordbSessionInfo)} while {nameof(CorDebug)} was not null");
            ManagedCallback = null;

            disposed = true;
        }

        private bool WaitForCriticalFailureThread()
        {
            Thread localCriticalFailureThread = null;

            //If we've already started a critical failure thread, we must wait for that to end before we try and dispose
            lock (criticalFailureThreadLock)
            {
                if (criticalFailureThread != null)
                {
                    //Don't wait for the thread to end if we _are_ the critical failure thread!
                    if (Thread.CurrentThread.ManagedThreadId != criticalFailureThread.ManagedThreadId)
                    {
                        localCriticalFailureThread = criticalFailureThread;
                    }
                }
            }

            //We must do this outside of the lock, so that if the critical failure thread itself tries to call WaitForCriticalFailureThread we don't deadlock with us waiting for it and it waiting for us (so that it can enter the lock)
            if (localCriticalFailureThread != null)
            {
                //todo: not sure why i commented this out

                //If we're the engine thread, just leave it to the critical failure thread to cleanup
                //if (Thread.CurrentThread.ManagedThreadId == EngineThread.ManagedThreadId)
                //    return false;

                localCriticalFailureThread.Join();

                lock (criticalFailureThread)
                    criticalFailureThread = null;
            }

            return true;
        }
    }
}
