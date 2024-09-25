using System;
using System.Diagnostics;
using System.Threading;
using ChaosLib;
using ClrDebug;
using Win32Process = System.Diagnostics.Process;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void ThreadProc(LaunchTargetOptions options)
        {
            //Unlike DbgEng, we don't really have any setup that we need to do; our CordbSessionInfo has been set,
            //so let's go ahead and notify the user that engine is initialized
            RaiseEngineInitialized();

            var localSession = Session;

            Debug.Assert(localSession != null, "Session was null");

            try
            {
                CreateDebugTarget(options, localSession.EngineCancellationToken);
            }
            catch (Exception ex)
            {
                //We can't assert that we must be terminating if we get to this point; the startup failure could have been as something as simple
                //as a bad image path was specified

                Log.Error<CordbEngine>(ex, "Failed to create debug target: {message}", ex.Message);

                localSession.TargetCreated.SetException(ex);

                //The UI thread is waiting for TargetCreated to be set. Cancel our CTS and shutdown the thread

                StopAndTerminate();

                localSession.Dispose();

                throw;
            }

            try
            {
                localSession.TargetCreated.SetResult();

                while (!localSession.IsEngineCancellationRequested) //temp
                {
                    localSession.EngineThread.Dispatcher.DrainQueue();

                    Thread.Sleep(100); //temp
                }
            }
            catch (Exception ex)
            {
                RaiseEngineFailure(new EngineFailureEventArgs(ex, EngineFailureStatus.BeginShutdown));

                throw;
            }
            finally
            {
                StopAndTerminate();
            }
        }

        private void StopAndTerminate()
        {
            //If we haven't already detached from the target process, terminate it now
            var localProcess = Process;
            var localSession = Session;

            if (localProcess != null)
            {
                Debug.Assert(localSession != null, "Session was null");

                //This will hang if the target process is zombified
                //We currently make the assumption that the debugger thread inside of the target won't be interrupted
                var hr = localProcess.CorDebugProcess.TryStop(0);

                switch (hr)
                {
                    case HRESULT.S_OK:
                        break;

                    case HRESULT.CORDBG_E_PROCESS_TERMINATED:
                        localSession.IsTerminated = true;
                        break;

                    default:
                        hr.ThrowOnNotOK();
                        break;
                }

                //When we get our ExitProcess event, we'll terminate our ICorDebug
                Terminate();
            }
            else
                Terminate(); //If we crashed during early startup, we might have a CorDebug but not a CorDebugProcess
        }

        private void CreateDebugTarget(LaunchTargetOptions options, CancellationToken token)
        {
            var localSession = Session;

            Debug.Assert(localSession != null, "Session was null");

            if (options.IsAttach)
            {
                Log.Information<CordbEngine>("Attaching to target process {targetPID} (interop: {interop})", options.ProcessId, options.UseInterop);

                localSession.IsAttaching = true;

                CordbLauncher.Attach(options, this, token);
            }
            else
            {
                Log.Information<CordbEngine>("Launching process {commandLine} (interop: {interop})", options.CommandLine, options.UseInterop);

                //Is the target executable a .NET Framework or .NET Core process?

                //We need to modify the environment variables too, so may as well just clone it
                options = options.Clone();

                options.FrameworkKind ??= services.FrameworkTypeDetector.Detect(options.CommandLine);

                localSession.DidCreateProcess = true;

                CordbLauncher.Create(options, this, token);
            }
        }
    }
}
