using System;
using System.Threading;

namespace ChaosDbg.Cordb
{
    //Engine startup/state/general code

    public partial class CordbEngine : ICordbEngine, IDbgEngineInternal, IDisposable
    {
        #region State

        public CordbProcess Process => Session?.Process;

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
                services,
                () => ThreadProc(options),
                cancellationToken
            );

            //We must start the debugger thread AFTER the Session variable has been assigned to
            Session.Start();

            //Once the target has been created, all the core values will exist
            //in our Session that are required to interact with the target
            Session.TargetCreated.Wait(cancellationToken);
        }

        #region Shutdown

        /* Termination
         *
         * ICorDebug::Terminate cleans up all remaining resources in your debugging session.
         * For each process in the ICorDebug process list, it checks whether that the process
         * CordbProcess::IsSafeToSendEvents(): - i.e. !m_terminated && !m_detached.
         *
         * If the process is still running under the debugger, because the process has not been terminated
         * or detached from, ICorDebug::Terminate will return CORDBG_E_ILLEGAL_SHUTDOWN_ORDER. Otherwise,
         * it is safe to shutdown, and the CordbRCEventThread will be terminated.
         *
         * CordbProcess::Terminate() sets m_exiting = true, and nicely asks the native pipeline to terminate
         * the process. Whether the process is actually terminated immediately or not can depend on a number of
         * factors; ultimately, the process is not truly terminated until EXIT_PROCESS_DEBUG_EVENT is received.
         * CordbWin32EventThread::Win32EventLoop() only waits 25ms each time it checks for debug events; after which
         * it then checks whether any of the process handles it monitors have been signalled (meaning they've exited).
         * If so, it calls CordbWin32EventThread::ExitProcess(fDetach: false) -> CordbProcess::Terminating(fDetach: false) which sets m_terminated = true
         * Then, at the very end of CordbWin32EventThread::ExitProcess(), ExitProcessWorkItem is queued onto the CordbRCEventThread,
         * which then calls ICorDebugManagedCallback::ExitProcess. The process will have already been marked as m_terminated at this point,
         * and when we return it will also be marked as neutered.
         *
         *
         * CordbProcess::Detach() does the following:
         * - Calls CordbProcess::DetachShim() which
         *   - calls CordbWin32EventThread::SendDetachProcessEvent() which sets m_action = W32ETA_DETACH and signals m_threadControlEvent
         *   - sets m_detached = true (unless the process has already terminated)
         * - Calls CordbProcess::Neuter() -> ShimProcess::Dispose() -> CordbWin32EventThread::Stop()
         *
         * The W32ETA_DETACH event is then handled inside of CordbWin32EventThread::Win32EventLoop(). As stated above,
         * this method only waits up to 25ms for events from WaitForDebugEvent(), after which it checks whether
         * any events it monitors have been signalled. m_threadControlEvent will have been signalled, and we'll end up
         * calling CordbWin32EventThread::ExitProcess(fDetach: true) -> CordbProcess::Terminating(fDetach: true)
         *
         * Thus, it would seem that regardless of whether you detach or terminate from the target, m_terminated is always
         * set to true. */

        /// <summary>
        /// Detaches from the target process and shuts down the ICorDebug services used by the current engine.
        /// </summary>
        public void Detach()
        {
            if (Process != null)
            {
                if (Process.Win32Process.HasExited)
                {
                    /* There is a huge race when detaching. If the process was just killed and we try and call detach, CordbProcess::DetachShim() will try and send
                     * an IPC event DB_IPCE_DETACH_FROM_PROCESS. CordbRCEventThread::SendIPCEvent waits on 3 event handles for a response: a right-side-acknowledgement-handle,
                     * the handle of the remote process, and the handle to the debugger thread in the remote process. If either the remote process or its debugger thread
                     * have ended, CORDBG_E_PROCESS_TERMINATED will be returned.
                     *
                     * Now we're in really big trouble however, because CordbProcess::Neuter() will then ALWAYS be immediately called, which will then
                     * call ShimProcess::Dispose() -> CordbWin32EventThread::Stop(). However, we need the CordbWin32EventThread to set m_terminated = true
                     * and to call our ExitProcess event handler. If we shutdown this process before CordbWin32EventThread has realized its terminated,
                     * we'll never know that the process has ended. And since m_detached won't have been set to true (CordbProcess::DetachShim() will
                     * have thrown after SendIPCEvent failed) what does this mean for trying to call ICorDebug::Terminate()? Contrary to what the comments in
                     * Cordb::Terminate() say, processes are NOT guaranteed to be removed from m_processes prior to CordbProcess::Detach() returning.
                     * Cordb::RemoveProcess() is called from CordbWin32EventThread::ExitProcess(), and the whole issue here is that if the CordbWin32EventThread
                     * is shutdown prematurely, normal shutdown bookkeeping won't be performed. */

                    //To work around the above issue, if we see that we've already exited, don't detach (which will neuter and risk breaking everything),
                    //instead call Terminate and wait for the ExitProcess event to be fired
                    Terminate();
                }
                else
                {
                    /* ICorDebug does not support detach in interop scenarios, and we can't call DebugActiveProcessStop directly,
                     * because this function must be called on the CordbWin32EventThread. We don't have wany way of injecting ourselves
                     * onto that thread, and don't even know how safe this would be to do even if we could do it. If we are interop debugging,
                     * the call to Detach() will fail */

                    //While we did already check whether the process has exited, if it were to exit prior to detach returning,
                    //we'll be in big trouble, because if CordbWin32EventThread didn't detect the process exited prior to the process
                    //being neutered, ICorDebug::Terminate will throw. For now, we don't handle this rare scenario
                    Process.CorDebugProcess.Detach();

                    //Now clear out the process so we don't attempt to kill it when this engine is destroyed
                    Session.Process.Dispose();
                    Session.Process = null;

                    //We want to now assert that there are no special timing rules we need to adhere to, and that it is safe for us to shutdown ICorDebug ASAP. There's
                    //nothing left for this engine to do once we no longer have a process, anyway.
                    if (Session.CorDebug != null)
                    {
                        Session.CorDebug.Terminate();
                        Session.CorDebug = null;
                    }
                }
            }
        }

        /// <summary>
        /// Terminates the target process and shuts down the ICorDebug services used by the current engine.
        /// </summary>
        public void Terminate()
        {
            if (Process != null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                Process.Terminate();
#pragma warning restore CS0618 // Type or member is obsolete

                //Now clear out the process so we don't attempt to kill it when this engine is destroyed
                Session.Process.Dispose();
                Session.Process = null;
            }

            //CordbProcess.Terminate() should wait for the ExitProcess event to be emitted, and so we now want to assert that it is safe for us to shutdown ICorDebug ASAP.
            //There's nothing left for this engine to do once we no longer have a process, anyway.
            if (Session.CorDebug != null)
            {
                Session.CorDebug.Terminate();
                Session.CorDebug = null;
            }
        }

        #endregion

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
                    Process.Win32Process.Kill();
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
