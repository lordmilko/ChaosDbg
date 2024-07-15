using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChaosDbg.DbgEng.Server;
using ChaosLib;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Stores debugger entities used to manage the current <see cref="DbgEngEngine"/> session.
    /// </summary>
    public class DbgEngSessionInfo : IDbgSessionInfo, IDisposable
    {
        public DbgEngProcess ActiveProcess
        {
            get => Processes.ActiveProcess;
            set => Processes.ActiveProcess = value;
        }

        public DbgEngProcessStore Processes { get; private set; }

        /// <inheritdoc cref="IDbgSessionInfo.EventFilters" />
        public DbgEngEventFilterStore EventFilters { get; }

        public EngineStatus Status { get; set; }

        /// <summary>
        /// Gets the <see cref="DebugClient"/> that is associated with the current thread.
        /// </summary>
        public DebugClient ActiveClient
        {
            get
            {
                var tid = Thread.CurrentThread.ManagedThreadId;

                //We store the engine client separately to indicate it's special, and provide fast lookup
                if (tid == EngineThreadId)
                    return EngineClient;

                //This is some type of UI thread
                if (TryGetUiClient(tid, out var client))
                    return client.Client;

                throw new InvalidOperationException("Could not find a DebugClient that is associated with the current thread");
            }
        }

        private Dictionary<int, SafeDebugClient> uiClientCache = new Dictionary<int, SafeDebugClient>();
        private object uiClientCacheLock = new object();

        private bool TryGetUiClient(int tid, out SafeDebugClient client)
        {
            lock (uiClientCacheLock)
            {
                if (uiClientCache.TryGetValue(tid, out var safeDebugClient))
                {
                    client = safeDebugClient;
                    return true;
                }
            }

            client = null;
            return false;
        }

        #region EngineClient

        private DebugClient engineClient;

        /// <summary>
        /// Gets the <see cref="DebugClient"/> that should be invoked inside the engine thread.
        /// </summary>
        public DebugClient EngineClient
        {
            get
            {
                //We don't store this in uiClientCache to denote that it's special

                if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                    throw new DebugEngineException($"Attempted to access {nameof(EngineClient)} outside of the engine thread, however it should only be accessed from within the engine thread.");

                return engineClient;
            }
        }

        /// <summary>
        /// Gets a pointer to the <see cref="EngineClient"/> contained in this session container.<para/>
        /// While this property can be accessed from any thread, this value should only be used for the purposes of passing the pointer to other DbgEng methods that may want to send a notification to the <see cref="EngineClient"/>,
        /// such as the UI Client sending a notification that the <see cref="EngineClient"/> should stop waiting for events.
        /// </summary>
        public IntPtr EngineClientRaw => engineClient?.Raw ?? IntPtr.Zero;

        /// <summary>
        /// Initializes <see cref="EngineClient"/> by creating a new <see cref="DebugClient"/> from UI Client for use
        /// on the <see cref="EngineThread"/>.
        /// </summary>
        /// <param name="options">Information about the debug target that should be launched.</param>
        public void CreateEngineClient(LaunchTargetOptions options)
        {
            if (Thread.CurrentThread.ManagedThreadId != EngineThread.ManagedThreadId)
                throw new DebugEngineException($"{nameof(EngineClient)} should only be created on the engine thread");

            if (engineClient != null)
                throw new InvalidOperationException("Cannot create engine client: engine client has already been previously created");

            /* DbgEng says that DebugClient instances can only be used on the threads that create them. IDebugClient.CreateClient
             * says that calls to DbgEng made on a thread other than the one that created a given DebugClient will fail immediately
             * (https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/dbgeng/nf-dbgeng-idebugclient-createclient). This
             * isn't strictly true, however there are a few scenarios where DbgEng checks the ID of the thread that owns a given DebugClient
             * when deciding which DebugClient to use, and will also send APCs to every thread that has an associated DebugClient (which we
             * disable via an import hook). The most important place where the owning thread seems to matter is the one that pumps the event
             * queue. As such, we'll follow DbgEng's advice and ensure we have a unique client per thread. */

            if (options.Kind == LaunchTargetKind.Server && options.ServerConnectionInfo.Kind == DbgEngServerKind.Debugger)
            {
                /* We want to create a client for connecting to a legacy Debugger Server (wherein an instance of WinDbg is acting
                 * as the server, as opposed to  dedicated "relay" (i.e. "process server") using DbgSrv.
                 * All of the "real" debugger work will occur in the remote Debugger Server. All this client will do is send and receive
                 * events from it. Because multiple clients could potentially connect to the Debugger Server, we have no way of guaranteeing
                 * that we are 100% synchronized with the Debugger Server. While we're trying to load all events upon attach, because we're not
                 * watching for new events, we might miss the fact that yet another module was loaded! As such, we can't rely on having cached
                 * modules/threads/etc, because as soon as we query this information, it could be out of date. Thus, we'll have to ask for this
                 * information from scratch every time we need it.
                 *
                 * You might think it might be worth asking how a command like dx @$curprocess.Modules works; does it provide a secret means of
                 * tapping into a cached location that we can get cached modules from? No, because this command won't be executed in our process,
                 * it'll be executed in the Debugger Server process, where it has direct access to the modules contained in the dbgeng!ProcessInfo */

                //If we're connecting to a remote server, we need to use DebugConnect if it's a Debugger Server, and DebugCreate + ConnectProcessServer if it's a Process Server
                engineClient = services.SafeDebugConnect(options.ServerConnectionInfo.ClientConnectionString, options.UseDbgEngSymOpts ?? true);
            }
            else
            {
                //DebugClient instances can only be used on the threads that create them. As we need a background thread to
                //perform the work of waitng on and interacting with DbgEng, we need a new client for the engine thread from
                //our existing UiClient which was created initially from DebugCreate().
                lock (uiClientCacheLock)
                {
                    engineClient = uiClientCache.Values.First().UnsafeGetClient().CreateClient();
                    Log.Debug<DebugClient>("Created engine DebugClient {hashCode}", engineClient.GetHashCode());
                }
            }
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

        internal DbgEngEventHistoryStore EventHistory { get; } = new DbgEngEventHistoryStore();

        public TaskCompletionSource TargetCreated { get; } = new TaskCompletionSource();

        public TaskCompletionSource BreakEvent { get; } = new TaskCompletionSource();

        private DbgEngEngineServices services;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbgEngSessionInfo"/> class.
        /// </summary>
        /// <param name="services">Services that provide enhanced capabilities to the debugger.</param>
        /// <param name="threadProc">The procedure that should be executed inside of the engine thread.</param>
        /// <param name="uiClient">The <see cref="DebugClient"/> that should be invoked on the UI thread.</param>
        /// <param name="dbgEngEngineId">An ID to be used to correlate the engine thread with other ChaosDbg engine threads</param>
        /// <param name="cancellationToken">A cancellation token that can be used to terminate the debugger engine.</param>
        public DbgEngSessionInfo(DbgEngEngineServices services, ThreadStart threadProc, DebugClient uiClient, int? dbgEngEngineId, CancellationToken cancellationToken)
        {
            if (threadProc == null)
                throw new ArgumentNullException(nameof(threadProc));

            if (uiClient == null)
                throw new ArgumentNullException(nameof(uiClient));

            this.services = services;

            Log.Debug<DebugClient>("Created UI DebugClient {hashCode}", uiClient.GetHashCode());

            EngineThread = new DispatcherThread(
                $"DbgEng Engine Thread" + (dbgEngEngineId == null ? null : $" {dbgEngEngineId}"),
                threadProc,
                enableLog: true);

            lock (uiClientCacheLock)
            {
                var tid = Thread.CurrentThread.ManagedThreadId;
                uiClientCache[tid] = new SafeDebugClient(uiClient);
            }

            //Allow either the user to request cancellation via their token, or our session to request cancellation upon being disposed
            EngineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Processes = new DbgEngProcessStore(this, services);
            EventFilters = new DbgEngEventFilterStore(this);
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
                        ActiveClient.TryExitDispatch(EngineClientRaw);
                    else
                        ActiveClient.ExitDispatch(EngineClientRaw);
                }
            };
        }

        /// <summary>
        /// Executes a command using <see cref="BufferClient"/> that emits output to
        /// a clients' output callbacks.
        /// </summary>
        /// <param name="action">The action to perform using the <see cref="BufferClient"/>.</param>
        /// <returns>The output that was emitted to the output callbacks of the <see cref="BufferClient"/>.</returns>
        public string[] ExecuteBufferedCommand(Action<DebugClient> action)
        {
            return BufferOutput.Capture(
                () => action(BufferClient)
            );
        }

        #region IDbgSessionInfo

        private ExternalDbgProcessStore externalProcessStore;

        IDbgProcessStore IDbgSessionInfo.Processes => externalProcessStore ??= new ExternalDbgProcessStore(Processes);

        /// <inheritdoc />
        IDbgEventFilterStore IDbgSessionInfo.EventFilters => EventFilters;

        #endregion

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

            var tid = Thread.CurrentThread.ManagedThreadId;

            if (tid != EngineThreadId)
            {
                if (!TryGetUiClient(tid, out var uiClient))
                {
                    //Try any client then. If the engine thread is blocked, we need to wake it up!
                    lock (uiClientCacheLock)
                    {
                        uiClient = uiClientCache.Values.FirstOrDefault();
                    }
                }

                if (uiClient != null)
                {
                    //If the engine is currently waiting on DebugControl.WaitForEvent() inside EngineLoop, wake it up
                    //so it can see that cancellation was requested
                    uiClient.UnsafeGetClient().Control.TrySetInterrupt(DEBUG_INTERRUPT.ACTIVE);

                    //If the engine is currently waiting on DebugClient.DispatchCallbacks inside InputLoop, wake it up
                    //so it can see that cancellation was requested. The UiClient is set on the UI thread, while the EngineClient is set inside
                    //of the engine thread, so there's no guarantee that just because we have a UiClient we also have an EngineClient
                    if (EngineClientRaw != IntPtr.Zero)
                        uiClient.UnsafeGetClient().TryExitDispatch(EngineClientRaw);
                }
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

            lock (uiClientCacheLock)
            {
                foreach (var client in uiClientCache.Values)
                    client.Dispose();

                uiClientCache.Clear();
            }

            engineClient?.Dispose();
            bufferClient?.Dispose();

            engineClient = null;
            bufferClient = null;
            BufferOutput = null;

            Processes = null;
            externalProcessStore = null;
        }
    }
}
