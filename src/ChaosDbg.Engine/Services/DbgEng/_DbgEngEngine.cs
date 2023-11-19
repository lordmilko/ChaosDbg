using System;
using System.Threading;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Engine startup/state/general code

    public partial class DbgEngEngine : IDisposable
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

        /// <summary>
        /// Gets the container that manages the commands that should be dispatched and processed in the engine thread.
        /// </summary>
        private DbgEngCommandStore Commands { get; }

        #endregion

        private readonly NativeLibraryProvider nativeLibraryProvider;

        public DbgEngEngine(NativeLibraryProvider nativeLibraryProvider)
        {
            this.nativeLibraryProvider = nativeLibraryProvider;
            Commands = new DbgEngCommandStore(this);
        }

        /// <summary>
        /// Notifies the <see cref="EngineClient"/> that a command is available for processing.
        /// </summary>
        internal void WakeEngineForInput() => Session.UiClient.ExitDispatch(Session.EngineClientRaw);

        /// <summary>
        /// Launches the specified target and attaches the debugger to it.
        /// </summary>
        /// <param name="launchInfo">Information about the debug target that should be launched.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to terminate the debugger engine.</param>
        public void Launch(DbgLaunchInfo launchInfo, CancellationToken cancellationToken = default)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {launchInfo}: an existing session is already running.");

            Session = new DbgEngSessionInfo(
                () => ThreadProc(launchInfo),
                CreateDebugClient(),
                cancellationToken
            );

            Modules = new DbgEngModuleStore();
            Threads = new DbgEngThreadStore();

            Session.Start();
        }

        private DebugClient CreateDebugClient()
        {
            var debugCreate = nativeLibraryProvider.GetExport<DebugCreateDelegate>(WellKnownNativeLibrary.DbgEng, "DebugCreate");

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
