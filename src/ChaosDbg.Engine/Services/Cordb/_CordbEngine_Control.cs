using System;
using System.Diagnostics;
using ClrDebug;

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
            if (Session.CurrentStopCount == 0)
                return;

            //Keep calling CorDebugController.Continue() until we're freely running again
            while (Session.CurrentStopCount > 0)
                DoContinue(Process.CorDebugProcess, false);
        }

        private void DoContinue(CorDebugController controller, bool outOfBand, bool isUnmanaged = false)
        {
            //If this is the last stop, we're about to decrement the stop count to 0,
            //indicating that the process is now freely running
            if (Session.CurrentStopCount == 1)
                SetEngineStatus(EngineStatus.Continue);

            //As soon as we call Continue(), another event may be dispatched by the runtime.
            //As such, we must do all required bookkeeping beforehand
            Session.CurrentStopCount--;
            Session.TotalContinueCount++;

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
                return Session.UnmanagedCallback.InBandThread.Invoke(() => controller.TryContinue(false));
        }

        public void Break()
        {
            Process.CorDebugProcess.Stop(0);
            OnStopping();
            Session.CurrentStopCount++;
            SetEngineStatus(EngineStatus.Break);
        }
    }
}
