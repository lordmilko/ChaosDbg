using System;
using System.Threading;
using ChaosDbg.Metadata;

namespace ChaosDbg.Cordb
{
    //Engine startup/state/general code

    public partial class CordbEngine : IDbgEngine, IDisposable
    {
        #region State

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="CordbEngine"/> session.
        /// </summary>
        public CordbSessionInfo Session { get; private set; }

        /// <summary>
        /// Gets the current debug target. This property is set by the engine thread.
        /// </summary>
        public CordbTargetInfo Target { get; private set; }

        /// <summary>
        /// Gets the container containing the modules that have been loaded into the current process.
        /// </summary>
        public CordbModuleStore Modules { get; private set; }

        /// <summary>
        /// Gets the container containing the threads that have been loaded into the current process.
        /// </summary>
        public CordbThreadStore Threads { get; private set; }

        /// <summary>
        /// Gets the container that manages the commands that should be dispatched and processed in the engine thread.
        /// </summary>
        private CordbCommandStore Commands { get; }

        #endregion

        private readonly IExeTypeDetector exeTypeDetector;

        public CordbEngine(IExeTypeDetector exeTypeDetector)
        {
            this.exeTypeDetector = exeTypeDetector;
            Commands = new CordbCommandStore(this);
        }

        public void Launch(DbgLaunchInfo launchInfo, CancellationToken cancellationToken = default)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {launchInfo}: an existing session is already running.");

            Session = new CordbSessionInfo(
                () => ThreadProc(launchInfo),
                cancellationToken
            );

            Modules = new CordbModuleStore();
            Threads = new CordbThreadStore();

            Session.Start();
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
