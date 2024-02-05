using System;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Gets or sets the current status of the process.
        /// </summary>
        public EngineStatus Status { get; set; }

        /// <summary>
        /// Gets whether both managed and native code are being debugged in the process.
        /// </summary>
        public bool IsInterop { get; set; }

        private int currentStopCount;

        /// <summary>
        /// Gets the current number of times a managed callback has been entered or Stop() has been called
        /// without subsequently calling Continue(). Any time this value is not 0, the process is not freely running.
        /// </summary>
        public int CurrentStopCount
        {
            get => currentStopCount;
            set => Interlocked.Exchange(ref currentStopCount, value);
        }

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

        public ManualResetEventSlim TargetCreated { get; } = new ManualResetEventSlim(false);

        internal bool IsAttaching { get; set; }

        public TaskCompletionSource<object> WaitExitProcess { get; } = new TaskCompletionSource<object>();

        public CordbEngineServices Services { get; }

        private bool disposed;

        public CordbSessionInfo(CordbEngineServices services, ThreadStart threadProc, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            Services = services;

            EngineThread = new DispatcherThread("Cordb Engine Thread", threadProc);

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            CallbackContext = new CordbCallbackContext(this);
        }

        /// <summary>
        /// Starts the debug session by launching the engine thread.
        /// </summary>
        public void Start() => EngineThread.Start();

        public void Dispose()
        {
            if (disposed)
                return;

            TargetCreated.Dispose();

            //First, cancel the CTS if we have one
            EngineCancellationTokenSource?.Cancel();

            //Wait for the engine thread to end. If we're going to terminate the target process, we need to do this
            //prior to disposing our callbacks (since after they're disposed, they won't accept new events, which means
            //we'll miss our ExitProcess event)
            EngineThread.Dispose();

            //The unmanaged callback has thread responsible for dispatching
            //in band callbacks that needs to be disposed
            UnmanagedCallback?.Dispose();
            ManagedCallback?.Dispose();

            Process?.Dispose();

            //Clear out essential debugger objects

            //We should have already terminated and nullified our ICorDebug upon terminating the process, however
            //we terminate it again here just in case we introduce a bug where we don't terminate it
            CorDebug?.Terminate();
            CorDebug = null;
            ManagedCallback = null;

            disposed = true;
        }
    }
}
