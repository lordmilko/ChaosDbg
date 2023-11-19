using System;
using System.Threading;
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
        private Thread EngineThread { get; }

        private bool disposed;

        public CordbSessionInfo(ThreadStart threadProc, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            EngineThread = new Thread(threadProc)
            {
                Name = "Cordb Engine Thread"
            };

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        /// <summary>
        /// Starts the debug session by launching the engine thread.
        /// </summary>
        public void Start() => EngineThread.Start();

        public void Dispose()
        {
            if (disposed)
                return;

            //First, cancel the CTS if we have one
            EngineCancellationTokenSource?.Cancel();

            //Wait for the engine thread to end
            EngineThread.Join();

            //Clear out essential debugger objects
            CorDebug = null;
            ManagedCallback = null;

            disposed = true;
        }
    }
}
