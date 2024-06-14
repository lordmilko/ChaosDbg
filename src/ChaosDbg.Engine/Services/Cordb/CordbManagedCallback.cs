using System;
using System.Diagnostics;
using System.Threading;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbManagedCallback : CorDebugManagedCallback, IDisposable
    {
        public event EventHandler<CorDebugManagedCallbackEventArgs> OnPreEvent;

        private bool disposed;
        private bool crashing;
        private bool crashingOnAttach;
        private CountdownEvent entranceCount = new CountdownEvent(1);
        private ManualResetEventSlim gotCrashOnAttachEvent = new ManualResetEventSlim(false);

        private Stopwatch waitForEventTimer = new Stopwatch();
        private Stopwatch processEventTimer = new Stopwatch();

        public Action<Exception, EngineFailureStatus> OnEngineFailure { get; set; }

        public CordbManagedCallback()
        {
            waitForEventTimer.Start();
        }

        protected override HRESULT HandleEvent<T>(EventHandler<T> handler, CorDebugManagedCallbackEventArgs args)
        {
            Log.Debug<CordbManagedCallback>("Got {kind}", args.Kind);

            waitForEventTimer.Stop();
            Log.Debug<CordbUnmanagedCallback, Stopwatch>("Waited {elapsed} to get {eventType}", waitForEventTimer.Elapsed, args.Kind);
            processEventTimer.Restart();

            if (disposed)
                return HRESULT.E_FAIL;

            if (!entranceCount.TryAddCount())
                return HRESULT.E_FAIL;

            /* If an unhandled exception occurs, we need to request that the engine
             * be stopped and that the ICorDebug/ICorDebugProcess be terminated.
             * The ICorDebugProcess won't be marked as terminated within mscordbi
             * until the ExitProcess event has been received. Since we can't guarantee
             * what state the debugger will be in when an unhandled exception occurs,
             * we begin skipping all managed callbacks we receive until we get to the
             * ExitProcess event, which will set a signal that we're good to begin
             * shutting everything down. */

            var isExitProcess = args.Kind == CorDebugManagedCallbackKind.ExitProcess || args.Kind == CorDebugManagedCallbackKind.DebuggerError;

            try
            {
                OnPreEvent?.Invoke(this, args);

                if (!crashing || isExitProcess)
                    handler?.Invoke(this, (T) args);

                if (crashingOnAttach)
                {
                    //We simply needed to wait for QueueFakeAttachEventsIfNeeded to return, so that we can exit FlushQueuedEvents and process the ExitProcessWorkItem
                    //we're about to enqueue
                    args.Continue = false;

                    gotCrashOnAttachEvent.Set();

                }
            }
            catch (Exception ex)
            {
                //We've encountered an unhandled exception. Raise a fatal error

                //If an unhandled exception occurred during our ExitProcess event, we're in mega trouble now,
                //because the UI is going to be waiting on that event to fire to know that ICorDebug can be shutdown
                OnEngineFailure?.Invoke(ex, isExitProcess ? EngineFailureStatus.ShutdownFailure : EngineFailureStatus.BeginShutdown);

                crashing = true;
            }

            try
            {
                //Regardless of whether an exception occurred during the event specific handler, we need to do our best to Continue()
                //so that we can try and receive the ExitProcess event that we'll need in order to shut everything down
                RaiseOnAnyEvent(args);
            }
            catch (Exception ex)
            {
                //We're in big trouble now. Most likely Continue() failed. All we can do is inform the UI and hope they can deal with it

                OnEngineFailure?.Invoke(ex, EngineFailureStatus.ShutdownFailure);
            }
            finally
            {
                entranceCount.Signal();
            }

            processEventTimer.Stop();
            Log.Debug<CordbManagedCallback, Stopwatch>("{eventType} completed in {elapsed}", args.Kind, processEventTimer.Elapsed);
            waitForEventTimer.Restart();

            return HRESULT.S_OK;
        }

        internal ManualResetEventSlim SetHadStartupFailure(bool attaching)
        {
            crashing = true;
            crashingOnAttach = attaching;

            if (attaching)
                return gotCrashOnAttachEvent;

            return null;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            //We need to ensure that the callback is not dispatching any events before we allow Dispose to end

            disposed = true;

            entranceCount.Signal();
            entranceCount.Wait();

            entranceCount.Dispose();

            gotCrashOnAttachEvent.Dispose();
        }
    }
}
