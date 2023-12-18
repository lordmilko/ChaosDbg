using System;
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
        private CountdownEvent entranceCount = new CountdownEvent(1);

        public CordbUnmanagedCallback()
        {
            InBandThread = new DispatcherThread($"{nameof(CordbUnmanagedCallback)} In Band Thread");
            InBandThread.Start();
        }

        HRESULT ICorDebugUnmanagedCallback.DebugEvent(ref DEBUG_EVENT pDebugEvent, bool fOutOfBand)
        {
            if (disposed)
                return HRESULT.E_FAIL;

            //If we fail to set the count, it means Dispose has already decremented the count to 0
            if (!entranceCount.TryAddCount())
                return HRESULT.E_FAIL;

            var unmanagedEventArgs = new DebugEventCorDebugUnmanagedCallbackEventArgs(pDebugEvent, fOutOfBand);

            OnPreEvent?.Invoke(this, unmanagedEventArgs);

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

            return HRESULT.S_OK;
        }

        private void HandleEvent<T>(EventHandler<T> handler, T handlerEventArgs, DebugEventCorDebugUnmanagedCallbackEventArgs eventArgs)
        {
            handler?.Invoke(this, handlerEventArgs);

            OnAnyEvent?.Invoke(this, eventArgs);
        }

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
