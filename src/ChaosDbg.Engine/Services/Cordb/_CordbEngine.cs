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

        public CordbProcess Process => Session.Process;

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="CordbEngine"/> session.
        /// </summary>
        public CordbSessionInfo Session { get; private set; }

        #endregion

        private readonly IExeTypeDetector exeTypeDetector;
        private readonly CordbEngineServices services;
        private bool disposed;

        public CordbEngine(
            IExeTypeDetector exeTypeDetector,
            NativeLibraryProvider nativeLibraryProvider,
            CordbEngineServices services)
        {
            //Ensure the right DbgHelp gets loaded before we need it
            nativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            this.exeTypeDetector = exeTypeDetector;
            this.services = services;
        }

        public void CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default) =>
            CreateSession(options, cancellationToken);

        public void Attach(AttachProcessOptions options, CancellationToken cancellationToken = default) =>
            CreateSession(options, cancellationToken);

        private void CreateSession(object options, CancellationToken cancellationToken)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {options}: an existing session is already running.");

            Session = new CordbSessionInfo(
                () => ThreadProc(options),
                cancellationToken
            );

            //We must start the debugger thread AFTER the Session variable has been assigned to
            Session.Start();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            //To avoid races with the managed callback thread, we say that we're disposed
            //immediately just in case an event comes in
            disposed = true;

            try
            {
                if (Process != null)
                    System.Diagnostics.Process.GetProcessById(Process.Id).Kill();
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
