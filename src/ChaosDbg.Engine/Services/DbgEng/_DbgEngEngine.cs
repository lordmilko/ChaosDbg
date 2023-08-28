using System;
using System.Threading;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Engine startup/state/general code

    public partial class DbgEngEngine
    {
        /// <summary>
        /// Gets the current <see cref="EngineClient"/>. This property should only be accessed on the engine thread.
        /// </summary>
        public DebugClient EngineClient => Session.EngineClient;

        /// <summary>
        /// Gets whether the engine has been cancelled and is in the process of shutting down.
        /// </summary>
        public bool IsEngineCancellationRequested => Session.IsEngineCancellationRequested;

        #region State

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="DbgEngEngine"/> session.
        /// </summary>
        public DbgEngSessionInfo Session { get; private set; }

        /// <summary>
        /// Gets the current debug target. This property is set by the engine thread.
        /// </summary>
        public DbgEngTargetInfo Target { get; private set; }

        #endregion

        private readonly NativeLibraryProvider nativeLibraryProvider;

        public DbgEngEngine(NativeLibraryProvider nativeLibraryProvider)
        {
            this.nativeLibraryProvider = nativeLibraryProvider;
        }

        /// <summary>
        /// Launches the specified target and attaches the debugger to it.
        /// </summary>
        /// <param name="launchInfo">Information about the debug target that should be launched.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to terminate the debugger engine.</param>
        public void Launch(DbgEngLaunchInfo launchInfo, CancellationToken cancellationToken = default)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {launchInfo}: an existing session is already running.");

            Session = new DbgEngSessionInfo(
                () => ThreadProc(launchInfo),
                CreateDebugClient(),
                cancellationToken
            );

            Session.Start();
        }

        private DebugClient CreateDebugClient()
        {
            var debugCreate = nativeLibraryProvider.GetExport<DebugCreateDelegate>(WellKnownNativeLibrary.DbgEng, "DebugCreate");

            debugCreate(DebugClient.IID_IDebugClient, out var pDebugClient).ThrowOnNotOK();

            var debugClient = new DebugClient(pDebugClient);

            return debugClient;
        }
    }
}
