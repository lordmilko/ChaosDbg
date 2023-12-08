using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbUnmanagedCallback : ICorDebugUnmanagedCallback
    {
        public DispatcherThread InBandThread { get; }

        public EventHandler<bool> OnAnyEvent;

        public EventHandler<EXCEPTION_DEBUG_INFO> OnException;
        public EventHandler<CREATE_PROCESS_DEBUG_INFO> OnCreateProcess;
        public EventHandler<EXIT_PROCESS_DEBUG_INFO> OnExitProcess;
        public EventHandler<CREATE_THREAD_DEBUG_INFO> OnCreateThread;
        public EventHandler<EXIT_THREAD_DEBUG_INFO> OnExitThread;
        public EventHandler<LOAD_DLL_DEBUG_INFO> OnLoadDll;
        public EventHandler<UNLOAD_DLL_DEBUG_INFO> OnUnloadDll;
        public EventHandler<OUTPUT_DEBUG_STRING_INFO> OnDebugString;
        public EventHandler<RIP_INFO> OnRipInfo;

        public CordbUnmanagedCallback()
        {
            InBandThread = new DispatcherThread($"{nameof(CordbUnmanagedCallback)} In Band Thread");
        }

        HRESULT ICorDebugUnmanagedCallback.DebugEvent(ref DEBUG_EVENT pDebugEvent, bool fOutOfBand)
        {
            switch (pDebugEvent.dwDebugEventCode)
            {
                case DebugEventType.EXCEPTION_DEBUG_EVENT:
                    HandleEvent(OnException, pDebugEvent.u.Exception, fOutOfBand);
                    break;

                case DebugEventType.CREATE_PROCESS_DEBUG_EVENT:
                    HandleEvent(OnCreateProcess, pDebugEvent.u.CreateProcess, fOutOfBand);
                    break;

                case DebugEventType.EXIT_PROCESS_DEBUG_EVENT:
                    HandleEvent(OnExitProcess, pDebugEvent.u.ExitProcess, fOutOfBand);
                    break;

                case DebugEventType.CREATE_THREAD_DEBUG_EVENT:
                    HandleEvent(OnCreateThread, pDebugEvent.u.CreateThread, fOutOfBand);
                    break;

                case DebugEventType.EXIT_THREAD_DEBUG_EVENT:
                    HandleEvent(OnExitThread, pDebugEvent.u.ExitThread, fOutOfBand);
                    break;

                case DebugEventType.LOAD_DLL_DEBUG_EVENT:
                    HandleEvent(OnLoadDll, pDebugEvent.u.LoadDll, fOutOfBand);
                    break;

                case DebugEventType.UNLOAD_DLL_DEBUG_EVENT:
                    HandleEvent(OnUnloadDll, pDebugEvent.u.UnloadDll, fOutOfBand);
                    break;

                case DebugEventType.OUTPUT_DEBUG_STRING_EVENT:
                    HandleEvent(OnDebugString, pDebugEvent.u.DebugString, fOutOfBand);
                    break;

                case DebugEventType.RIP_EVENT:
                    HandleEvent(OnRipInfo, pDebugEvent.u.RipInfo, fOutOfBand);
                    break;
                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(DebugEventType)} '{pDebugEvent.dwDebugEventCode}'.");
            }

            return HRESULT.S_OK;
        }

        private void HandleEvent<T>(EventHandler<T> handler, T eventArgs, bool fOutOfBand)
        {
            handler?.Invoke(this, eventArgs);

            OnAnyEvent?.Invoke(this, fOutOfBand);
        }

        public void Dispose()
        {
            InBandThread.Dispose();
        }
    }
}
