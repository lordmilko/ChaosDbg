using System;
using System.Diagnostics;
using System.Threading;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbUnmanagedCallback : ICorDebugUnmanagedCallback
    {
        public DispatcherThread InBandThread { get; }

        public event EventHandler<DebugEventCorDebugUnmanagedCallbackEventArgs> OnAnyEvent;
        public event EventHandler<DebugEventCorDebugUnmanagedCallbackEventArgs> OnPreEvent;

        public event EventHandler<EXCEPTION_DEBUG_INFO> OnException;
        public event EventHandler<CREATE_PROCESS_DEBUG_INFO> OnCreateProcess;
        public event EventHandler<EXIT_PROCESS_DEBUG_INFO> OnExitProcess;
        public event EventHandler<CREATE_THREAD_DEBUG_INFO> OnCreateThread;
        public event EventHandler<EXIT_THREAD_DEBUG_INFO> OnExitThread;
        public event EventHandler<LOAD_DLL_DEBUG_INFO> OnLoadDll;
        public event EventHandler<UNLOAD_DLL_DEBUG_INFO> OnUnloadDll;
        public event EventHandler<OUTPUT_DEBUG_STRING_INFO> OnDebugString;
        public event EventHandler<RIP_INFO> OnRipInfo;

        private bool disposed;
        private bool crashing;
        private CountdownEvent entranceCount = new CountdownEvent(1);

        private Stopwatch waitForEventTimer = new Stopwatch();
        private Stopwatch processEventTimer = new Stopwatch();

        public Action<Exception, EngineFailureStatus> OnEngineFailure { get; set; }

        public CordbUnmanagedCallback()
        {
            InBandThread = new DispatcherThread($"{nameof(CordbUnmanagedCallback)} In Band Thread", enableLog: true);
            InBandThread.Start();

            waitForEventTimer.Start();
        }

        HRESULT ICorDebugUnmanagedCallback.DebugEvent(ref DEBUG_EVENT pDebugEvent, bool fOutOfBand)
        {
            Log.Debug<CordbUnmanagedCallback>("Got {kind}", pDebugEvent.dwDebugEventCode);

            waitForEventTimer.Stop();
            Log.Debug<CordbUnmanagedCallback, Stopwatch>("Waited {elapsed} to get {eventType}", waitForEventTimer.Elapsed, pDebugEvent.dwDebugEventCode);
            processEventTimer.Restart();

            /* Sometimes the debugger might be stopped and we might be trying to interact with a native thread,
             * when suddenly we might receive an out of band event informing us that the thread has now terminated!
             * This can be very annoying when trying to do things such as interact with the TEB. So should we just
             * block the unmanaged callback thread when the debugger is supposed to be paused and dispatch any out
             * of band events when we're ready to continue? NO. ICorDebug APIs apparently become blocked while waiting
             * for out of band events to occur. Thus, if we were to try and stop the debuggee like we would when
             * debugging a native application, the debugger will deadlock
             * https://web.archive.org/web/20140505033705/http://blogs.msdn.com/b/jmstall/archive/2005/09/13/out-of-band-events.aspx */

            if (disposed)
                return HRESULT.E_FAIL;

            //If we fail to set the count, it means Dispose has already decremented the count to 0
            if (!entranceCount.TryAddCount())
                return HRESULT.E_FAIL;

            var unmanagedEventArgs = new DebugEventCorDebugUnmanagedCallbackEventArgs(pDebugEvent, fOutOfBand);

            try
            {
                //This can throw
                OnPreEvent?.Invoke(this, unmanagedEventArgs);
            }
            catch (Exception ex)
            {
                //We've encountered an unhandled exception. Raise a fatal error.
                //We swallow this error so we can still pump events, and have CordbEngine_Events request that we perform an engine stop

                OnEngineFailure?.Invoke(ex, EngineFailureStatus.BeginShutdown);

                crashing = true;
            }

            try
            {
                switch (pDebugEvent.dwDebugEventCode)
                {
                    case DebugEventType.EXCEPTION_DEBUG_EVENT:
                        HandleEvent(OnException, pDebugEvent.u.Exception, unmanagedEventArgs);
                        break;

                    case DebugEventType.CREATE_PROCESS_DEBUG_EVENT:
                        HandleEvent(OnCreateProcess, pDebugEvent.u.CreateProcess, unmanagedEventArgs);
                        break;

                    case DebugEventType.EXIT_PROCESS_DEBUG_EVENT:
                        HandleEvent(OnExitProcess, pDebugEvent.u.ExitProcess, unmanagedEventArgs);
                        break;

                    case DebugEventType.CREATE_THREAD_DEBUG_EVENT:
                        HandleEvent(OnCreateThread, pDebugEvent.u.CreateThread, unmanagedEventArgs);
                        break;

                    case DebugEventType.EXIT_THREAD_DEBUG_EVENT:
                        HandleEvent(OnExitThread, pDebugEvent.u.ExitThread, unmanagedEventArgs);
                        break;

                    case DebugEventType.LOAD_DLL_DEBUG_EVENT:
                        HandleEvent(OnLoadDll, pDebugEvent.u.LoadDll, unmanagedEventArgs);
                        break;

                    case DebugEventType.UNLOAD_DLL_DEBUG_EVENT:
                        HandleEvent(OnUnloadDll, pDebugEvent.u.UnloadDll, unmanagedEventArgs);
                        break;

                    case DebugEventType.OUTPUT_DEBUG_STRING_EVENT:
                        HandleEvent(OnDebugString, pDebugEvent.u.DebugString, unmanagedEventArgs);
                        break;

                    case DebugEventType.RIP_EVENT:
                        HandleEvent(OnRipInfo, pDebugEvent.u.RipInfo, unmanagedEventArgs);
                        break;
                    default:
                        throw new NotImplementedException($"Don't know how to handle {nameof(DebugEventType)} '{pDebugEvent.dwDebugEventCode}'.");
                }
            }
            finally
            {
                //Handles get closed in ShimProcess::TrackFileHandleForDebugEvent() which is
                //only called by ShimProcess::DefaultEventHandler() when not interop debugging.
                //Process and thread handles do not need to be closed

                switch (pDebugEvent.dwDebugEventCode)
                {
                    case DebugEventType.CREATE_PROCESS_DEBUG_EVENT:
                        Kernel32.CloseHandle(pDebugEvent.u.CreateProcess.hFile);
                        break;

                    case DebugEventType.LOAD_DLL_DEBUG_EVENT:
                        Kernel32.CloseHandle(pDebugEvent.u.LoadDll.hFile);
                        break;
                }

                entranceCount.Signal();
            }

            processEventTimer.Stop();
            Log.Debug<CordbUnmanagedCallback, Stopwatch>("{eventType} completed in {elapsed}", pDebugEvent.dwDebugEventCode, processEventTimer.Elapsed);
            waitForEventTimer.Restart();

            return HRESULT.S_OK;
        }

        private void HandleEvent<T>(EventHandler<T> handler, T handlerEventArgs, DebugEventCorDebugUnmanagedCallbackEventArgs eventArgs)
        {
            //If we've had an unhandled exception, just call Continue until we get the _managed_ ExitProcess event. We don't need to care about
            //the _unmanaged_ ExitProcess event. We don't care about the unmanaged CreateProcess event either: our thread name/logging context
            //is initialized inside CordbLauncher
            if (!crashing)
            {
                try
                {
                    handler?.Invoke(this, handlerEventArgs);
                }
                catch (Exception ex)
                {
                    //We've encountered an unhandled exception. Raise a fatal error.
                    //We swallow this error so we can still pump events, and have CordbEngine_Events request that we perform an engine stop

                    OnEngineFailure?.Invoke(ex, EngineFailureStatus.BeginShutdown);

                    crashing = true;
                }
            }
            else
                Log.Debug<CordbUnmanagedCallback>("Not handling event {eventType} because unmanaged callback is crashing", eventArgs.DebugEvent.dwDebugEventCode);

            try
            {
                OnAnyEvent?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                //We're in big trouble now. Most likely Continue() failed. All we can do is inform the UI and hope they can deal with it

                OnEngineFailure?.Invoke(ex, EngineFailureStatus.ShutdownFailure);
            }
        }

        internal void SetHadStartupFailure() => crashing = true;

        public void Dispose()
        {
            if (disposed)
                return;

            //We need to ensure that the Win32 event thread is not dispatching any events before we allow Dispose to end

            disposed = true;

            //The initial count was 1, now it's 0. There's now three possibilities:
            //1. No unmanaged callbacks are running. If a callback is dispatched it will see disposed = true and return
            //2. An unmanaged callback is running. When it gets to the end it will signal entrance count, bringing it to 0 and setting the signal
            //3. A race is occurring where an unmanaged callback has passed the disposed check but has not yet tried to increment the signal. TryAdd will fail and the callback will abort
            entranceCount.Signal();
            entranceCount.Wait();

            //It is now guaranteed that no more unmanaged events will fire
            entranceCount.Dispose();

            InBandThread.Dispose();
        }
    }
}
