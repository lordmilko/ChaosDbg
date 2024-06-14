using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using ChaosDbg.DbgEng;
using ChaosLib;

namespace ChaosDbg.Cordb
{
    //Engine startup/state/general code

    public partial class CordbEngine : ICordbEngine, IDbgEngineInternal, IDisposable
    {
        #region State

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> that is associated with this <see cref="CordbEngine"/>.<para/>
        /// Unlike <see cref="DbgEngEngine"/>, each <see cref="CordbEngine"/> can only be associated with a singular <see cref="CordbProcess"/>.
        /// </summary>
        public CordbProcess Process => Session?.ActiveProcess;

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="CordbEngine"/> session.
        /// </summary>
        public CordbSessionInfo Session { get; private set; }

        #endregion

        private readonly CordbEngineServices services;
        private readonly CordbEngineProvider engineProvider;
        private object terminateLock = new object();
        private bool disposed;

        public CordbEngine(CordbEngineServices services, CordbEngineProvider engineProvider)
        {
            //Ensure the right DbgHelp gets loaded before we need it
            services.NativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            this.services = services;
            this.engineProvider = engineProvider;
        }

        [Obsolete("Do not call this method. Use CordbEngineProvider.CreateProcess() instead")]
        void IDbgEngineInternal.CreateProcess(CreateProcessTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use CordbEngineProvider.Attach() instead")]
        void IDbgEngineInternal.Attach(AttachProcessTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use CordbEngineProvider.OpenDump() instead")]
        void IDbgEngineInternal.OpenDump(OpenDumpTargetOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private void CreateSession(LaunchTargetOptions options, CancellationToken cancellationToken)
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

            /* Once the target has been created, all the core values will exist
             * in our Session that are required to interact with the target.
             * We only pass the caller's CancellationToken here, as if a fatal exception occurs during debugger startup,
             * we'll set the exception as a result of the TCS, causing it to be re-thrown on the UI thread. If we passed in
             * the merged engine CancellationToken, when we cancel that token while handling the fatal exception, that will
             * cause a cancelled exception to be thrown here, which is the wrong exception. */
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

        #region Shutdown

        /* Termination
         * --------------------------------------------------------------
         *
         * ----------------------------------
         * ICorDebug
         * ----------------------------------
         *
         * Terminate
         * ---------------
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
         * factors; ultimately, the process is not truly terminated (as far as mscordbi is concerned) until the managed
         * ExitProcess event is received is received. CordbWin32EventThread::Win32EventLoop() only waits 25ms each time
         * it checks for debug events; after which it then checks whether any of the process handles it monitors have been
         * signalled (meaning they've exited). If so, it calls
         *
         *     CordbWin32EventThread::ExitProcess(fDetach: false) -> CordbProcess::Terminating(fDetach: false)
         *
         * which sets m_terminated = true
         *
         * Then, at the very end of CordbWin32EventThread::ExitProcess(), ExitProcessWorkItem is queued onto the CordbRCEventThread,
         * which then calls ICorDebugManagedCallback::ExitProcess. The process will have already been marked as m_terminated at this point,
         * and when we return it will also be marked as neutered.
         *
         * Terminate (when CordbRCEventThread is already in the middle of preparing to dispatch an event)
         * ----------------------------------------------------------------------------------------------
         *
         * There is a race that can occur when attempting to shutdown early when attaching (technically speaking it can happen even when not attaching
         * too, but its most common when attaching).
         *
         * Ostensibly, what's normally meant to happen is that at the very end of CordbWin32EventThread::ExitProcess(), ExitProcessWorkItem is queued onto
         * the CordbRCEventThread (in m_WorkerStack). This queue gets drained via a call to CordbRCEventThread::DrainWorkerQueue() at the top of
         * CordbRCEventThread::ThreadProc(). However, when attaching, during the point at which the Win32 Event Thread is marking the process as terminated,
         * the CordbRCEventThread may already be in the middle of preparing to execute its first event, ShimProxyCallback::QueueCreateProcess().
         *
         * CordbRCEventThread::ThreadProc() calls into CordbRCEventThread::FlushQueuedEvents() which, before dispatching the event via CordbProcess::DispatchRCEvent(),
         * it calls ShimProcess::QueueFakeAttachEventsIfNeeded(). Since this is the first event that's been received, m_fNeedFakeAttachEvents is still set
         * from CordbWin32EventThread::AttachProcess() -> ShimProcess::BeginQueueFakeAttachEvents() setting it to true. Thus, while QueueFakeAttachEventsIfNeeded() is
         * in the middle of executing, we're sending a termination request to the Win32 event thread. CordbWin32EventThread::AttachProcess() also calls Cordb::AddProcess
         * which signals CordbRCEventThread::ProcessStateChanged(), causing the CordbRCEventThread to see that m_processStateChanged is now set.
         *
         * ShimProcess::QueueFakeAttachEvents() will likely be in the middle of calling ShimProcess::QueueFakeThreadAttachEventsNativeOrder(), which will suddenly find that
         * its unable to enumerate the threads in the target process, and return a ReadVirtual failure, which will silently be ignored. When QueueFakeAttachEventsIfNeeded
         * finally returns, FlushQueuedevents will proceed with attempting to call CordbProcess::DispatchRCEvent() to process the ShimProxyCallback::QueueCreateProcess() event.
         *
         * However, by this point, the Win32 event thread has now marked the process as terminated, and added the ExitProcessWorkItem to the CordbRCEventThread::m_WorkerStack.
         * But CordbRCEventThread doesn't know this yet, as it's already in the middle of processing another event! And so, when CordbProcess::DispatchRCEvent() tries to init
         * its StopContinueHolder, this calls CordbProcess::StopInternal() which sees that m_terminated is now true, and so returns CORDBG_E_PROCESS_TERMINATED.
         *
         * Seeing this, CordbProcess::DispatchRCEvent() freaks out and dispatches an CordbProcess::UnrecoverableError(), triggering the DebuggerError callback. It seems that the only
         * place that the DebuggerError callback can be triggered is from inside CordbProcess::UnrecoverableError() (ShimProxyCallback::DebuggerError doesn't ever seem to be called;
         * the ShimProxyCallback seems to only be called via GetShimCallback()->[callbackName]
         *
         * Thus, we must respond to process termination events coming from both ExitProcess and DebuggerError events. While this race is particularly common
         * in attach scenarios, technically speaking it can happen even in create scenarios as well if the Win32 event thread marks the process as terminated while
         * the CordbRCEventThread hasn't yet finished calling CordbProcess::StopInternal().
         *
         * The downside of receiving a DebuggerError event instead of an ExitProcess event, is that the CordbProcess object is only neutered at the end of ExitProcessWorkItem::Do.
         * Since the CordbProcess never gets neutered, ShimProcess::Dispose() never gets called, which is required in order to terminate the Win32 Event Thread. Thus, when we
         * get a DebuggerError event for CORDBG_E_PROCESS_TERMINATED, we've leaked a ShimProcess and a Win32 Event Thread. To mitigate this, when crashing during early
         * startup, if we see that we're attaching, we'll wait until we get the first managed event so that we can stop the managed event thread and then signal that we're ready
         * to stop and terminate. This way, we know that the managed event thread is no longer in the middle of preparing to execute a request, and so won't explode
         * when it hits DispatchRCEvent.
         *
         * Detach
         * ------
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
         * set to true.
         *
         * ----------------------------------
         * ChaosDbg
         * ----------------------------------
         *
         * ChaosDbg can be shutdown either gracefully (via the UI thread) or unexpectedly (via a crash on either the managed or unmanaged thread).
         * The hardest part about managing shutdowns is dealing with scenarios where the engine has crashed. Ideally, we want to be able to
         * totally cleanup our ICorDebug and ICorDebugProcess instances, terminating and disposing of their underlying COM objects properly.
         *
         * Startup Crashes
         * ---------------
         *
         * When a crash occurs during engine startup, the UI thread will be waiting for the TargetCreated event to be set. CordbEngine.ThreadProc
         * employs logic to say that if the debug target cannot be properly created, an exception is set on the TargetCreated TCS (thereby propagating
         * this exception to the UI thread) and then the CordbSessionInfo is disposed of immediately.
         *
         * Event Thread Crashes
         * --------------------
         *
         * After startup, if an unhandled exception occurs on either Cordb Engine Thread, the Managed Callback Thread or the Win32 Callback Thread,
         * our OnEngineFailure event will be triggered, forcing the UI to somehow deal with this fatal exception. In the case of our callback threads,
         * that's all they do. However our Cordb Engine Thread goes one step further: having broken out of the core engine loop, it also initiates a shutdown
         * of the target process, calling TryStop() and then CordbEngine.Terminate()
         */

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
                    lock (terminateLock)
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
                        Session.ActiveProcess.Dispose();
                        Session.ActiveProcess = null;

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
        }

        /// <summary>
        /// Terminates the target process and shuts down the ICorDebug services used by the current engine.
        /// </summary>
        public void Terminate()
        {
            lock (terminateLock)
            {
                if (Process != null)
                {
                    Log.Verbose("Terminating and disposing CorDebugProcess");

#pragma warning disable CS0618 // Type or member is obsolete
                    Process.Terminate();
#pragma warning restore CS0618 // Type or member is obsolete

                    //Now clear out the process so we don't attempt to kill it when this engine is destroyed
                    Session.ActiveProcess.Dispose();
                    Session.ActiveProcess = null;
                }

                //CordbProcess.Terminate() should wait for the ExitProcess event to be emitted, and so we now want to assert that it is safe for us to shutdown ICorDebug ASAP.
                //There's nothing left for this engine to do once we no longer have a process, anyway.
                if (Session.CorDebug != null)
                {
                    Log.Verbose("Terminating CorDebug...");

                    Session.CorDebug.Terminate();
                    Session.CorDebug = null;

                    Log.Verbose("Terminated CorDebug");
                }
            }
        }

        #endregion

        private void CheckIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CordbEngineProvider));
        }

        #region IDbgEngine

        IDbgProcess IDbgEngine.ActiveProcess => Process;

        IDbgSessionInfo IDbgEngine.Session => Session;

        #endregion

        public void Dispose()
        {
            if (disposed)
                return;

            Log.Verbose("Disposing CordbEngine");

            //The process should have been properly cleaned up when we terminated/detached from it, but dispose again here just in case
            Process?.Dispose();

            //To avoid races with the managed callback thread, we say that we're disposed
            //immediately just in case an event comes in
            disposed = true;

            engineProvider.Remove(this);

            try
            {
                if (Process != null && !Process.Win32Process.HasExited)
                    Process.Win32Process.Kill();
            }
            catch
            {
                //Ignore
            }

            //Terminate() will be called when the engine thread ends

            Session?.Dispose();
            Session = null;
        }
    }
}
