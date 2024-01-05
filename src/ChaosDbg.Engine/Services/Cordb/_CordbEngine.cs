﻿using System;
using System.Threading;
using ChaosDbg.Metadata;
using Win32Process = System.Diagnostics.Process;

namespace ChaosDbg.Cordb
{
    //Engine startup/state/general code

    public partial class CordbEngine : ICordbEngine, IDbgEngineInternal, IDisposable
    {
        #region State

        public CordbProcess Process => Session.Process;

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="CordbEngine"/> session.
        /// </summary>
        public CordbSessionInfo Session { get; private set; }

        #endregion

        private readonly CordbEngineServices services;
        private bool disposed;

        public CordbEngine(CordbEngineServices services)
        {
            //Ensure the right DbgHelp gets loaded before we need it
            services.NativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            this.services = services;
        }

        [Obsolete("Do not call this method. Use CordbEngineProvider.CreateProcess() instead")]
        void IDbgEngineInternal.CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use CordbEngineProvider.Attach() instead")]
        void IDbgEngineInternal.Attach(AttachProcessOptions options, CancellationToken cancellationToken) =>
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

            //Once the target has been created, all the core values will exist
            //in our Session that are required to interact with the target
            Session.TargetCreated.Wait(cancellationToken);
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
                    Win32Process.GetProcessById(Process.Id).Kill();
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
