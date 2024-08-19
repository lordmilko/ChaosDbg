using System;
using System.Diagnostics;
using ChaosDbg.Disasm;
using ChaosLib;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        public void Invoke(Action action)
        {
            CheckIfDisposed();

            Session.EngineThread.Invoke(action);
        }

        /// <summary>
        /// User-level resumes the current process.<para/>
        /// This method will repeatedly call <see cref="CorDebugController.Continue(bool)"/> until
        /// <see cref="CordbSessionInfo.UserPauseCount"/> is 0, indicating that the process is now
        /// freely running.
        /// </summary>
        public void Continue()
        {
            lock (Session.UserPauseCountLock)
            {
                Log.Debug<CordbEngine>("Attempting to Continue");

                CheckIfDisposed();

                //If we're already running, nothing to do!
                if (Session.UserPauseCount == 0)
                {
                    Log.Debug<CordbEngine>("Ignoring Continue as target is already running");
                    return;
                }

                var lastPause = Session.EventHistory.LastStopReason;

                //If we paused as a result of a native event (especially if it was an out of band one) we need to continue that event first
                if (lastPause is CordbNativeEventPauseReason nativePause)
                {
                    Session.UserPauseCount--;

                    Log.Debug<CordbEngine>("Last pause had a native reason. Continuing Win32 Event Thread prior to Continuing (remaining pause count: {pauseCount})", Session.UserPauseCount);

                    DoContinue(Process.CorDebugProcess, nativePause.OutOfBand, true);
                }

                //Keep calling CorDebugController.Continue() until we're freely running again
                while (Session.UserPauseCount > 0)
                {
                    Session.UserPauseCount--;

                    Log.Debug<CordbEngine>("Continuing managed event thread (remaining pause count: {pauseCount}", Session.UserPauseCount);

                    DoContinue(Process.CorDebugProcess, false);
                }
            }
        }

        private void DoContinue(CorDebugController controller, bool outOfBand, bool isUnmanaged = false)
        {
            try
            {
                //If this is the last stop, we're about to decrement the stop count to 0,
                //indicating that the process is now freely running
                if (Session.CallbackStopCount == 1)
                {
                    if (!Session.IsCrashing && !Session.IsTerminating)
                    {
                        //These items are nice to haves, but if the engine is in the middle of crashing, don't bother with them, as
                        //we can't guarantee whether we have an accurate picture of the processes state (not to mention we intentionally
                        //skip event callbacks while in the middle of crashing)

                        /* If the process is already terminated, it's entirely possible that we don't even have a Process object anymore.
                         * e.g. suppose the UI thread is trying to cleanup the process. We're in the middle of handling the ExitProcess managed
                         * debug event. We just set IsTerminated = true and signalled the WaitExitProcess event. The UI thread is like "great, I'm
                         * going to remove the ActiveProcess now", however we then race with it and trip over it null being null */

                        //And if we're in the middle of terminating (but not yet fully terminated), we already cleared out our threads, so we can't clear any interrupts or update register contexts

                        Debug.Assert(Process != null, "Expected to have an active process");

                        Process.Breakpoints.RestoreCurrentBreakpoint();

                        if (isUnmanaged)
                        {
                            //If this is an ExitThread event, the event thread won't exist anymore

                            TryClearHardInterrupt();
                        }

                        //Apply any modified register contexts
                        Process.Threads.SaveRegisterContexts();
                    }

                    //Invalidate any data we collected while we were paused
                    Session.PauseContext.Clear();
                }

                //As soon as we call Continue(), another event may be dispatched by the runtime.
                //As such, we must do all required bookkeeping beforehand
                Session.CallbackStopCount--;
                Session.TotalContinueCount++;

                //This must be called _after_ updating the stop count, as the Status is automatically derived from it
                NotifyEngineStatus();
            }
            catch (Exception ex)
            {
                Debug.Assert(false, "We should never throw when attempting to call DoContinue(). When we're handling an unmanaged event like EXIT_PROCESS_DEBUG_EVENT, failing to continue means that the remaining thread handles won't be closed, which means the process won't ever terminate, and we'll hang forever");
            }

            HRESULT hr;

            if (isUnmanaged)
            {
                Log.Debug<CordbEngine>("Continuing (unmanaged: {unmanaged}, outOfband: {outOfBand})", true, outOfBand);
                hr = TryUnmanagedContinue(controller, outOfBand);
            }
            else
            {
                Debug.Assert(!outOfBand);
                Log.Debug<CordbEngine>("Continuing (unmanaged: {unmanaged})", false);
                hr = controller.TryContinue(false);
            }

            switch (hr)
            {
                case HRESULT.CORDBG_E_PROCESS_TERMINATED:
                    //If the process wasn't marked as terminated before, it is now!
                    Session.IsTerminated = true;
                    break;

                case HRESULT.CORDBG_E_SUPERFLOUS_CONTINUE:
                    //If a managed callback crashed in its primary handler, it will raise a critical failure, and CordbProcess.Terminate() will call Continue() as many times as necessary from the critical
                    //failure thread in order to get the target going. When the managed callback that crashed loses the race to try and Continue() itself (because the critical failure thread already did a continue
                    //for it) it will fail with a superflous continue. This is OK; we just need to make sure we get the exit process event so that we can cleanup
                    if (Session.IsTerminating)
                    {
                        //I don't think we can assert that we're necessarily crashing at this point. There can be a race between calling Terminate() and a managed event that was already
                        //in the process of running (e.g. we had a LoadModule event that was still in progress). Upon doing a bunch of continues in Terminate(), the LoadModule event
                        //goes to Continue and trips over the fact that we've continued too many times on purpose
                        break;
                    }

                    //If we're not terminating, this is a bug
                    hr.ThrowOnNotOK();
                    break;

                default:
                    hr.ThrowOnNotOK();
                    break;
            }
        }

        private HRESULT TryUnmanagedContinue(CorDebugController controller, bool fOutOfBand)
        {
            /* Normally, Win32 Debuggers must call ::ContinueDebugEvent() after handling a debug event.
             * If we're not doing interop debugging, ShimProcess::HandleWin32DebugEvent() calls WindowsNativePipeline::ContinueDebugEvent()
             * which immediately calls ::ContinueDebugEvent(). Given we are interop debugging however, is it our responsibility to do that?
             * And does that answer change depending on whether it's an in or out of band event or not? The answer is no, and no.
             *
             * When we have an OOB event, CordbProcess::Continue(true) calls CordbProcess::ContinueOOB()
             * CorDbProcess::ContinueOOB() sets m_dispatchingOOBEvent to false.
             *
             * OOB callbacks are typically called from CordbProcess::HandleDebugEventForInteropDebugging().
             * After the callback has been invoked, m_dispatchingOOBEvent is checked. As stated above, because Continue(true)
             * was called, the value is false, and so CordbWin32EventThread::UnmanagedContinue() is called.
             * CordbWin32EventThread::UnmanagedContinue() calls CordbWin32EventThread::DoDbgContinue() which gets
             * the dwContinueStatus to use for continuing the event, before finally calling WindowsNativePipeline::ContinueDebugEvent()
             * which calls ::ContinueDebugEvent()
             *
             * For inband events, our callback is invoked from CordbProcess::DispatchUnmanagedInBandEvent().
             * Calling Continue(false) sets m_dispatchingUnmanagedEvent to false. If m_dispatchingUnmanagedEvent is still true after the callback ends,
             * CordbProcess::DispatchUnmanagedInBandEvent() returns early, and CordbWin32EventThread::DoDbgContinue() is not called, ultimately meaning
             * that ::ContinueDebugEvent() is not called either. The documentation for ICorDebugController::Continue states that Continue() should not
             * be called from the Win32 thread unless its an OOB event. Continue() _does_ need to be called however, just from a different thread.
             * CordbProcess::ContinueInternal() states that calling Continue() could cause IPC events to occur, which would be an issue on the win32
             * event thread (I assume because the win32 event thread is already busy trying to continue, so it might cause a deadlock?)
             *
             * The macro CORDBFailIfOnWin32EventThread() is invoked to verify we're not trying to Continue() from the Win32 thread. If we are,
             * CORDBG_E_CANT_CALL_ON_THIS_THREAD is returned. The solution, therefore, is to have another thread we can queue our Continue
             * request to to safely handle from there instead
             */

            if (fOutOfBand)
                return controller.TryContinue(true);
            else
            {
                /* I think it may be possible for this to deadlock. CordbWin32EventThread::SendUnmanagedContinue()
                 * dispatches a W32ETA_CONTINUE event to the win32 event thread, and then waits on m_actionTakenEvent.
                 * This event is set in CordbWin32EventThread::HandleUnmanagedContinue() in response to the
                 * W32ETA_CONTINUE. But if the win32 event thread is hung up waiting for this callback to respond,
                 * it's going to deadlock! */
                Debug.Assert(Session.UnmanagedCallback != null, "Didn't have an UnmanagedCallback set");
                Session.UnmanagedCallback.InBandThread.InvokeAsync(() => controller.TryContinue(false));
                return HRESULT.S_OK;
            }
        }

        private void TryClearHardInterrupt()
        {
            //Nothing to do if the thread no longer exists!
            if (Session.CallbackContext.UnmanagedEventType == DebugEventType.EXIT_THREAD_DEBUG_EVENT)
                return;

            /* If we weren't just handling a native exit thread event, it is guaranteed that the thread still exists.
             * We only get OOB ExitThread events when we've stopped during a managed callback. This method will only
             * be called if we're in the middle of an unmanaged callback, or if we stopped during an unmanaged callback,
             * in which case the Win32 Event Thread is blocked and we won't get an ExitThread event until we Continue */

            var eventThread = Session.CallbackContext.UnmanagedEventThread;

            bool SkipInt3(CordbThread thread)
            {
                var ip = thread.RegisterContext.IP;
                var disasm = Process.ProcessDisassembler.Disassemble(ip);

                if (disasm.Instruction.Code == Code.Int3)
                {
                    thread.RegisterContext.IP++;
                    return true;
                }

                return false;
            }

            //If the current IP is a hardcoded int3, we need to step over it
            if (eventThread == Process.Threads.ActiveThread)
            {
                SkipInt3(eventThread);
            }
            else
            {
                //We switched to another thread. But if the event thread is at an int 3, because we rewound the int 3 above so that
                //we could display it to the user, we need to manually fast forward past it

                SkipInt3(eventThread);
            }
        }

        public void Break()
        {
            CheckIfDisposed();

            //We currently make the assumption that the debugger thread inside of the target won't be interrupted
            Process.CorDebugProcess.Stop(0);
            Session.EventHistory.Add(new CordbUserBreakPauseReason());
            OnStopping(false);
            NotifyEngineStatus();
        }

        public HRESULT TryCreateBreakpoint(CordbILFunction function, int offset, out CordbManagedCodeBreakpoint breakpoint)
        {
            CheckIfDisposed();

            breakpoint = default;

            //Process.CorDebugProcess.Break
            var code = function.CorDebugFunction.NativeCode;

            if (offset > code.Size)
                throw new InvalidOperationException($"Cannot create native breakpoint inside managed code at offset {offset}: offset is greater than native code size ({code.Size})");

            //The created breakpoint is set to active prior to being returned from mscordbi
            var hr = code.TryCreateBreakpoint(offset, out var rawBreakpoint);

            if (hr != HRESULT.S_OK)
                return hr;

            Process.Breakpoints.Add(rawBreakpoint);
            return hr;
        }

        public void CreateNativeBreakpoint(CORDB_ADDRESS address)
        {
            CheckIfDisposed();

            //Info about stepping:
            //https://www.codereversing.com/archives/169
            //https://www.codereversing.com/archives/178

            Process.Breakpoints.Add(address);
        }

        public void CreateDataBreakpoint(long address, DR7.Kind accessKind, DR7.Length size)
        {
            CheckIfDisposed();

            Process.Breakpoints.Add(address, accessKind, size);
        }

        public void StepIntoNative()
        {
            CheckIfDisposed();

            Process.Breakpoints.AddNativeStep(null, false);
            Continue();
        }

        public void StepOverNative()
        {
            CheckIfDisposed();

            /* In DbgEng, when you do step into or over, this command is dispatched by ParseStepTrace to SetExecStepTrace()
             * The pivotal handling of step over vs step into is then handled in BaseX86MachineInfo::GetNextOffset(). When
             * stepping into, the global step breakpoint's address is set to either the address of the next instruction (when
             * stepping over) or nothing (when stepping into). This value then comes into play in dbgeng!InsertBreakpoints(),
             * where either MachineInfo::QuietSetTraceMode will be called (setting the trap flag) or the global step breakpoint
             * will be inserted (allowing for the step over) */

            Process.Breakpoints.AddNativeStep(null, true);
            Continue();
        }
    }
}
