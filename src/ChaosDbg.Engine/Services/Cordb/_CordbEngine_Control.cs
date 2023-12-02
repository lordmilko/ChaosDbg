using System;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        public void ExecuteCommand(Action action)
        {
            Commands.ExecuteInEngine(action);
        }

        /// <inheritdoc />
        public void Continue()
        {
            //If we're already running, nothing to do!
            if (Session.CurrentStopCount == 0)
                return;

            //Keep calling CorDebugController.Continue() until we're freely running again
            while (Session.CurrentStopCount > 0)
                DoContinue(Target.Process.CorDebugProcess);
        }

        private void DoContinue(CorDebugController controller)
        {
            //If this is the last stop, we're about to decrement the stop count to 0,
            //indicating that the process is now freely running
            if (Session.CurrentStopCount == 1)
                SetEngineStatus(EngineStatus.Continue);

            //As soon as we call Continue(), another event may be dispatched by the runtime.
            //As such, we must do all required bookkeeping beforehand
            Session.CurrentStopCount--;
            Session.TotalContinueCount++;

            var hr = controller.TryContinue(false);

            switch (hr)
            {
                case HRESULT.CORDBG_E_PROCESS_TERMINATED:
                    break;

                default:
                    hr.ThrowOnNotOK();
                    break;
            }
        }

        public void Break()
        {
            Target.Process.CorDebugProcess.Stop(0);
            OnStopping();
            Session.CurrentStopCount++;
            SetEngineStatus(EngineStatus.Break);
        }
    }
}
