using System;
using System.Threading;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Stores debugger entities used to manage the current <see cref="DbgEngEngine"/> session.
    /// </summary>
    public class DbgEngSessionInfo : IDisposable
    {
        #region UiClient

        private DebugClient uiClient;

        /// <summary>
        /// Gets the <see cref="DebugClient"/> that should be invoked on the UI thread.
        /// </summary>
        public DebugClient UiClient
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId == EngineThread.ManagedThreadId)
                    throw new DebugEngineException($"Attempted to access {nameof(UiClient)} on the engine thread, however {nameof(UiClient)} should only be accessed on the UI thread.");

                return uiClient;
            }
            set => uiClient = value;
        }

        #endregion
        #region EngineClient

        private DebugClient engineClient;

        /// <summary>
        /// Gets the <see cref="DebugClient"/> that should be invoked inside the engine thread.
        /// </summary>
        public DebugClient EngineClient
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                    throw new DebugEngineException($"Attempted to access {nameof(EngineClient)} outside of the engine thread, however it should only be accessed from within the engine thread.");

                return engineClient;
            }
            set => engineClient = value;
        }

        /// <summary>
        /// Initializes <see cref="EngineClient"/> by creating a new <see cref="DebugClient"/> from <see cref="UiClient"/> for use
        /// on the <see cref="EngineThread"/>.
        /// </summary>
        /// <returns>The <see cref="DebugClient"/> that should be used on the <see cref="EngineThread"/>.</returns>
        public DebugClient CreateEngineClient()
        {
            if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                throw new DebugEngineException($"{nameof(EngineClient)} should only be created on the engine thread");

            if (engineClient != null)
                throw new InvalidOperationException("Cannot create engine client: engine client has already been previously created");

            //DebugClient instances can only be used on the threads that create them. As we need a background thread to
            //perform the work of waitng on and interacting with DbgEng, we need a new client for the engine thread from
            //our existing UiClient which was created initially from DebugCreate().
            engineClient = uiClient.CreateClient();

            return engineClient;
        }

        #endregion

        /// <summary>
        /// Gets the <see cref="CancellationTokenSource"/> that can be used to terminate all running commands
        /// and shutdown the debug engine and wraps the <see cref="CancellationToken"/> that may have been supplied by the user.<para/>
        /// This property should not be exposed outside of the <see cref="DbgEngSessionInfo"/>. If we want to cancel our session,
        /// we should call <see cref="DbgEngSessionInfo.Dispose"/>, which will ensure the <see cref="EngineClient"/> can be notified
        /// to break out of any infinite waits (which we cannot otherwise interrupt).
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
        /// Stores a reference to the internal thread that the debugger engine is running on.
        /// </summary>
        private Thread EngineThread { get; }

        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbgEngSessionInfo"/> class.
        /// </summary>
        /// <param name="threadProc">The procedure that should be executed inside of the engine thread.</param>
        /// <param name="uiClient">The <see cref="DebugClient"/> that should be invoked on the UI thread.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to terminate the debugger engine.</param>
        public DbgEngSessionInfo(ThreadStart threadProc, DebugClient uiClient, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            if (uiClient == null)
                throw new ArgumentNullException(nameof(uiClient));

            EngineThread = new Thread(threadProc)
            {
                Name = "DbgEng Engine Thread"
            };

            UiClient = uiClient;

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        /// <summary>
        /// Starts the debug session by launching the engine thread.
        /// </summary>
        public void Start() => EngineThread.Start();

        /// <summary>
        /// Terminates the debugger session, sending notifications to DbgEng that it should wake up (if stuck inside of an internal wait)
        /// so that the engine thread can see that the engine is shutting down.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            //First, cancel the CTS if we have one
            EngineCancellationTokenSource?.Cancel();

            if (UiClient != null)
            {
                //If the engine is currently waiting on DebugControl.WaitForEvent() inside EngineLoop, wake it up
                //so it can see that cancellation was requested
                UiClient.Control.TrySetInterrupt(DEBUG_INTERRUPT.ACTIVE);

                //If the engine is currently waiting on DebugClient.DispatchCallbacks inside InputLoop, wake it up
                //so it can see that cancellation was requested. The UiClient is set on the UI thread, while the EngineClient is set inside
                //of the engine thread, so there's no guarantee that just because we have a UiClient we also have an EngineClient
                if (EngineClient != null)
                    UiClient.TryExitDispatch(EngineClient.Raw);
            }

            disposed = true;
        }
    }
}
