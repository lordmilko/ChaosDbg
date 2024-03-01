using System;
using System.Threading;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Engine startup/state/general code

    public partial class DbgEngEngine : IDbgEngineInternal, IDisposable
    {
        /// <summary>
        /// Gets the current <see cref="EngineClient"/>. This property should only be accessed on the engine thread.
        /// </summary>
        public DebugClient EngineClient => Session.EngineClient;

        /// <summary>
        /// Gets whether the engine has been cancelled and is in the process of shutting down.
        /// </summary>
        public bool IsEngineCancellationRequested => Session.IsEngineCancellationRequested;

        public DebugClient ActiveClient
        {
            get
            {
                var tid = Thread.CurrentThread.ManagedThreadId;

                if (tid == Session.EngineThreadId)
                    return Session.EngineClient;

                return Session.UiClient;
            }
        }

        #region State

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="DbgEngEngine"/> session.
        /// </summary>
        public DbgEngSessionInfo Session { get; private set; }

        /// <summary>
        /// Gets the current debug target. This property is set by the engine thread.
        /// </summary>
        public DbgEngTargetInfo Target { get; private set; }

        /// <summary>
        /// Gets the container containing the modules that have been loaded into the current process.
        /// </summary>
        public DbgEngModuleStore Modules { get; private set; }

        /// <summary>
        /// Gets the container containing the threads that have been loaded into the current process.
        /// </summary>
        public DbgEngThreadStore Threads { get; private set; }

        #endregion

        private readonly DbgEngEngineServices services;

        public DbgEngEngine(DbgEngEngineServices services)
        {
            this.services = services;

            Threads = new DbgEngThreadStore();
        }

        /// <summary>
        /// Notifies the <see cref="EngineClient"/> that a command is available for processing.
        /// </summary>
        internal void WakeEngineForInput() => Session.UiClient.ExitDispatch(Session.EngineClientRaw);

        [Obsolete("Do not call this method. Use DbgEngEngineProvider.CreateProcess() instead")]
        void IDbgEngineInternal.CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use DbgEngEngineProvider.Attach() instead")]
        void IDbgEngineInternal.Attach(AttachProcessOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        private void CreateSession(object options, CancellationToken cancellationToken)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {options}: an existing session is already running.");

            Session = new DbgEngSessionInfo(
                () => ThreadProc(options),
                CreateDebugClient(),
                cancellationToken
            );

            Modules = new DbgEngModuleStore(Session, services);

            //We must start the debugger thread AFTER the Session variable has been assigned to
            Session.Start();

            //Once the target has been created, all the core values will exist
            //in our Session that are required to interact with the target
            Session.TargetCreated.Wait(cancellationToken);
        }

        private DebugClient CreateDebugClient()
        {
            var debugCreate = services.NativeLibraryProvider.GetExport<DebugCreateDelegate>(WellKnownNativeLibrary.DbgEng, "DebugCreate");

            debugCreate(DebugClient.IID_IDebugClient, out var pDebugClient).ThrowOnNotOK();

            var debugClient = new DebugClient(pDebugClient);

            return debugClient;
        }

        public void Dispose()
        {
            Session?.Dispose();
            Session = null;
            Modules = null;
            Threads = null;
        }
    }
}
