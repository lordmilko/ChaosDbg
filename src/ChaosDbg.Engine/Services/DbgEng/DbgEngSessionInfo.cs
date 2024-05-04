using System;
using System.Threading;
using ChaosLib;
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
        }

        /// <summary>
        /// Gets a pointer to the <see cref="EngineClient"/> contained in this session container.<para/>
        /// While this property can be accessed from any thread, this value should only be used for the purposes of passing the pointer to other DbgEng methods that may want to send a notification to the <see cref="EngineClient"/>,
        /// such as the <see cref="UiClient"/> sending a notification that the <see cref="EngineClient"/> should stop waiting for events.
        /// </summary>
        public IntPtr EngineClientRaw => engineClient?.Raw ?? IntPtr.Zero;

        /// <summary>
        /// Initializes <see cref="EngineClient"/> by creating a new <see cref="DebugClient"/> from <see cref="UiClient"/> for use
        /// on the <see cref="EngineThread"/>.
        /// </summary>
        public void CreateEngineClient()
        {
            if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                throw new DebugEngineException($"{nameof(EngineClient)} should only be created on the engine thread");

            if (engineClient != null)
                throw new InvalidOperationException("Cannot create engine client: engine client has already been previously created");

            //DebugClient instances can only be used on the threads that create them. As we need a background thread to
            //perform the work of waitng on and interacting with DbgEng, we need a new client for the engine thread from
            //our existing UiClient which was created initially from DebugCreate().
            engineClient = uiClient.CreateClient();
            Log.Debug<DebugClient>("Created engine DebugClient {hashCode}", engineClient.GetHashCode());
        }

        #endregion
        #region BufferClient

        private DebugClient bufferClient;

        /// <summary>
        /// Gets a <see cref="DebugClient"/> that can be used for executing commands that emit their output to a clients' output callbacks.<para/>
        /// This client can only be used on the <see cref="EngineThread"/>.
        /// </summary>
        public DebugClient BufferClient
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                    throw new DebugEngineException($"Attempted to access {nameof(BufferClient)} outside of the engine thread, however it should only be accessed from within the engine thread.");

                return bufferClient;
            }
        }

        /// <summary>
        /// Initializes <see cref="BufferClient"/> by creating a new <see cref="DebugClient"/> from <see cref="EngineClient"/> for use
        /// on the <see cref="EngineThread"/>.
        /// </summary>
        public void CreateBufferClient()
        {
            bufferClient = EngineClient.CreateClient();
            Log.Debug<DebugClient>("Created buffer DebugClient {hashCode}", bufferClient.GetHashCode());
            BufferOutput = new BufferOutputCallbacks();

            //A variety of things are included in the default output mask. We only interested in output that we generate
            //bufferClient.OutputMask = 0;

            bufferClient.OutputCallbacks = BufferOutput;
        }

        #endregion

        /// <summary>
        /// Gets or sets whether a DbgEng command is currently blocked and waiting for input.
        /// </summary>
        public bool InputStarted { get; set; }

        private BufferOutputCallbacks BufferOutput { get; set; }

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
        /// Gets the managed thread ID of the <see cref="EngineThread"/>.
        /// </summary>
        public int EngineThreadId => EngineThread.ManagedThreadId;

        /// <summary>
        /// Stores a reference to the internal thread that the debugger engine is running on.
        /// </summary>
        internal DispatcherThread EngineThread { get; }

        public TaskCompletionSource TargetCreated { get; } = new TaskCompletionSource();

        public TaskCompletionSource BreakEvent { get; } = new TaskCompletionSource();

        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbgEngSessionInfo"/> class.
        /// </summary>
        /// <param name="threadProc">The procedure that should be executed inside of the engine thread.</param>
        /// <param name="uiClient">The <see cref="DebugClient"/> that should be invoked on the UI thread.</param>
        /// <param name="dbgEngEngineId">An ID to be used to correlate the engine thread with other ChaosDbg engine threads</param>
        /// <param name="cancellationToken">A cancellation token that can be used to terminate the debugger engine.</param>
        public DbgEngSessionInfo(ThreadStart threadProc, DebugClient uiClient, int? dbgEngEngineId, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            if (uiClient == null)
                throw new ArgumentNullException(nameof(uiClient));

            Log.Debug<DebugClient>("Created UI DebugClient {hashCode}", uiClient.GetHashCode());

            EngineThread = new DispatcherThread(
                $"DbgEng Engine Thread" + (dbgEngEngineId == null ? null : $" {dbgEngEngineId}"),
                threadProc,
                enableLog: true);

            UiClient = uiClient;

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        /// <summary>
        /// Starts the debug session by launching the engine thread.
        /// </summary>
        public void Start()
        {
            EngineThread.Start();

            EngineThread.Dispatcher.OperationQueued += (s, e) =>
            {
                if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                {
                    //If we're disposing, we're simply trying to dispatch the Shutdown command to the dispatcher
                    //thread. We don't care if ExitDispatch fails
                    if (disposed)
                        UiClient.TryExitDispatch(EngineClientRaw);
                    else
                        UiClient.ExitDispatch(EngineClientRaw);
                }
            };
        }

        /// <summary>
        /// Executes a command using <see cref="BufferClient"/> that emits output to
        /// a clients' output callbacks.
        /// </summary>
        /// <param name="action">The action to perform using the <see cref="BuferClient"/>.</param>
        /// <returns>The output that was emitted to the output callbacks of the <see cref="BufferClient"/>.</returns>
        public string[] ExecuteBufferedCommand(Action<DebugClient> action)
        {
            return BufferOutput.Capture(
                () => action(BufferClient)
            );
        }

        /// <summary>
        /// Terminates the debugger session, sending notifications to DbgEng that it should wake up (if stuck inside of an internal wait)
        /// so that the engine thread can see that the engine is shutting down.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

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
                if (EngineClientRaw != IntPtr.Zero)
                    UiClient.TryExitDispatch(EngineClientRaw);
            }

            EngineThread.Dispose();

            /* We need to remove our DebugClient instances from g_Clients. If we don't, if we create another
             * DebugClient in the future, many functions including NotifyChangeEngineState() will attempt to
             * notify our stale clients. Clients are removed from g_Clients in DebugClient::Unlink(), which is
             * only called via ~DebugClient(). Separate to this, DebugClient::Destroy() frees all resources
             * associated with a DebugClient, and disables any future callbacks on the client. Destroy() is called
             * after the final Release() but before the destructor runs */

            //Dispose() will clear out and dispose all RuntimeCallableWrapper members stored on the DebugClient,
            //which should bring the reference count to 0
            uiClient?.Dispose();
            engineClient?.Dispose();
            bufferClient?.Dispose();

            uiClient = null;
            engineClient = null;
            bufferClient = null;
            BufferOutput = null;
        }
    }
}
