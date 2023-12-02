using System;
using System.Diagnostics;
using System.Threading;
using ChaosDbg.Metadata;

namespace ChaosDbg.Cordb
{
    //Engine startup/state/general code

    public partial class CordbEngine : ICordbEngine, IDisposable
    {
        #region State

        public CordbProcess ActiveProcess => Session.Process;

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="CordbEngine"/> session.
        /// </summary>
        public CordbSessionInfo Session { get; private set; }

        /// <summary>
        /// Gets the current debug target. This property is set by the engine thread.
        /// </summary>
        public CordbTargetInfo Target { get; private set; }

        /// <summary>
        /// Gets the container that manages the commands that should be dispatched and processed in the engine thread.
        /// </summary>
        private CordbCommandStore Commands { get; }

        #endregion

        private readonly IExeTypeDetector exeTypeDetector;

        public CordbEngine(IExeTypeDetector exeTypeDetector, NativeLibraryProvider nativeLibraryProvider)
        {
            //Ensure the right DbgHelp gets loaded before we need it
            nativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            this.exeTypeDetector = exeTypeDetector;
            Commands = new CordbCommandStore(this);
        }

        public void CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {options}: an existing session is already running.");

            Session = new CordbSessionInfo(
                () => ThreadProc(options),
                cancellationToken
            );

            Session.Start();
        }

        public void Attach(AttachProcessOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            try
            {
                if (Target != null)
                    Process.GetProcessById(ActiveProcess.Id).Kill();
            }
            catch
            {
                //Ignore
            }

            Session?.Dispose();
            Session = null;
        }
    }
}
