using System;
using System.Runtime.ExceptionServices;
using System.Threading;
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
        private readonly DbgEngEngineProvider engineProvider;
        private bool disposed;

        public DbgEngEngine(DbgEngEngineServices services, DbgEngEngineProvider engineProvider)
        {
            this.services = services;
            this.engineProvider = engineProvider;

            Threads = new DbgEngThreadStore();
        }

        /// <summary>
        /// Notifies the <see cref="EngineClient"/> that a command is available for processing.
        /// </summary>
        internal void WakeEngineForInput() => Session.UiClient.ExitDispatch(Session.EngineClientRaw);

        [Obsolete("Do not call this method. Use DbgEngEngineProvider.CreateProcess() instead")]
        void IDbgEngineInternal.CreateProcess(LaunchTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use DbgEngEngineProvider.Attach() instead")]
        void IDbgEngineInternal.Attach(LaunchTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        private void CreateSession(LaunchTargetOptions options, CancellationToken cancellationToken)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {options}: an existing session is already running.");

            Session = new DbgEngSessionInfo(
                () => ThreadProc(options),
#pragma warning disable CS0618
                services.SafeDebugCreate(options.UseDbgEngSymOpts ?? true), //g_Machine is protected by the DbgEngEngineProvider
#pragma warning restore CS0618
                options.DbgEngEngineId,
                cancellationToken
            );

            Modules = new DbgEngModuleStore(Session, services);

            //We must start the debugger thread AFTER the Session variable has been assigned to
            Session.Start();

            try
            {
                //We want to be able to wait on the TCS with our CancellationToken, but this will result in an AggregateException being thrown on failure.
                //GetAwaiter().GetResult() won't let you use your CancellationToken in conjunction with them
                Session.TargetCreated.Task.Wait(cancellationToken);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            engineProvider.Remove(this);

            Session?.Dispose();
            Session = null;
            Modules = null;
            Threads = null;
        }
    }
}
