using System;
using System.Diagnostics;
using ChaosDbg.Disasm;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        public void Invoke(Action action) =>
            Session.EngineThread.Invoke(action);

        /// <inheritdoc />
        public void Continue()
        {
            //If we're already running, nothing to do!
            if (Session.UserPauseCount == 0)
                return;

            var lastPause = Session.EventHistory.LastStopReason;

            //If we paused as a result of a native event (especially if it was an out of band one) we need to continue that event first
            if (lastPause is CordbNativeEventPauseReason nativePause)
            {
                Session.UserPauseCount--;
                DoContinue(Process.CorDebugProcess, nativePause.OutOfBand, true);
            }

            //Keep calling CorDebugController.Continue() until we're freely running again
            while (Session.UserPauseCount > 0)
            {
                Session.UserPauseCount--;
                DoContinue(Process.CorDebugProcess, false);
            }
        }

        private void DoContinue(CorDebugController controller, bool outOfBand, bool isUnmanaged = false)
        {
            //If this is the last stop, we're about to decrement the stop count to 0,
            //indicating that the process is now freely running
            if (Session.CallbackStopCount == 1)
            {
                Process.Breakpoints.RestoreCurrentBreakpoint();

                if (isUnmanaged)
                {
                    //If this is an ExitThread event, the event thread won't exist anymore

                    TryClearHardInterrupt();
                }

                //Apply any modified register contexts
                Process.Threads.SaveRegisterContexts();

                //Invalidate any data we collected while we were paused
                Session.PauseContext.Clear();
            }

            //As soon as we call Continue(), another event may be dispatched by the runtime.
            //As such, we must do all required bookkeeping beforehand
            Session.CallbackStopCount--;
            Session.TotalContinueCount++;

            //This must be called _after_ updating the stop count, as the Status is automatically derived from it
            NotifyEngineStatus();

            HRESULT hr;

            if (isUnmanaged)
            {
                hr = TryUnmanagedContinue(controller, outOfBand);
            }
            else
            {
                Debug.Assert(!outOfBand);
                hr = controller.TryContinue(false);
            }

            switch (hr)
            {
                case HRESULT.CORDBG_E_PROCESS_TERMINATED:
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
            Process.CorDebugProcess.Stop(0);
            Session.EventHistory.Add(new CordbUserBreakPauseReason());
            OnStopping(false);
            NotifyEngineStatus();
        }

        public HRESULT TryCreateBreakpoint(CordbILFunction function, int offset, out CordbManagedCodeBreakpoint breakpoint)
        {
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
            //Info about stepping:
            //https://www.codereversing.com/archives/169
            //https://www.codereversing.com/archives/178

            Process.Breakpoints.Add(address);
        }

        public void CreateDataBreakpoint(long address, DR7.Kind accessKind, DR7.Length size)
        {
            Process.Breakpoints.Add(address, accessKind, size);
        }

        public void StepIntoNative()
        {
            Process.Breakpoints.AddNativeStep(null, false);
            Continue();
        }

        public void StepOverNative()
        {
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
