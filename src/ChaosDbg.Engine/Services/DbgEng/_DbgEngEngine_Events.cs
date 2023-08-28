using System;
using ClrDebug;
using ClrDebug.DbgEng;
using static ClrDebug.HRESULT;

namespace ChaosDbg.DbgEng
{
    //Engine event handlers and IDebug* DbgEng callbacks

    partial class DbgEngEngine : IDebugEventCallbacks, IDebugOutputCallbacks2
    {
        /// <summary>
        /// The event that occurs when the engine wishes to print output to the console.
        /// </summary>
        public event EventHandler<EngineOutputEventArgs> EngineOutput;

        /// <summary>
        /// The event that occurs when the debugger status changes (e.g. from broken to running).
        /// </summary>
        public event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged;

        #region IDebugEventCallbacks

        HRESULT IDebugEventCallbacks.GetInterestMask(out DEBUG_EVENT_TYPE mask)
        {
            mask =
                DEBUG_EVENT_TYPE.CHANGE_ENGINE_STATE; //Track when the debuggee state changes

            return S_OK;
        }

        DEBUG_STATUS IDebugEventCallbacks.Breakpoint(IntPtr bp) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.Exception(ref EXCEPTION_RECORD64 exception, int firstChance) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.CreateThread(long handle, long dataOffset, long startOffset) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.ExitThread(int exitCode) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.CreateProcess(long imageFileHandle, long handle, long baseOffset, int moduleSize, string moduleName,
            string imageName, int checkSum, int timeDateStamp, long initialThreadHandle, long threadDataOffset,
            long startOffset) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.ExitProcess(int exitCode) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.LoadModule(long imageFileHandle, long baseOffset, int moduleSize, string moduleName, string imageName,
            int checkSum, int timeDateStamp) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.UnloadModule(string imageBaseName, long baseOffset) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.SystemError(int error, int level) => DEBUG_STATUS.NO_CHANGE;

        HRESULT IDebugEventCallbacks.SessionStatus(DEBUG_SESSION status) => S_OK;

        HRESULT IDebugEventCallbacks.ChangeDebuggeeState(DEBUG_CDS flags, long argument) => S_OK;

        HRESULT IDebugEventCallbacks.ChangeEngineState(DEBUG_CES flags, long argument)
        {
            if (flags.HasFlag(DEBUG_CES.EXECUTION_STATUS))
            {
                //The debuggee's execution status is changing (e.g. going from running to broken
                //into). However, sometimes DbgEng may be working on stuff internally that inadvertently
                //causes a ChangeEngineState() event to occur but we don't actually need to react to it yet.
                //When this is the case, the INSIDE_WAIT flag will be specified
                bool hasInsideWait = (argument & DEBUG_STATUS.INSIDE_WAIT) != 0;

                if (hasInsideWait == false)
                {
                    var oldStatus = Target.Status; 

                    //It's a real event that we need to be notified of
                    var newStatus = (DEBUG_STATUS)argument;

                    //Only react to the status if it's different than our current status
                    if (oldStatus != newStatus)
                    {
                        //Something actually interesting has happened
                        Target.Status = newStatus;

                        //Notify any external subscribers (such as the UI) that the engine status ic changing
                        HandleEvent(EngineStatusChanged, new EngineStatusChangedEventArgs(oldStatus, newStatus));
                    }
                }
            }

            return S_OK;
        }

        HRESULT IDebugEventCallbacks.ChangeSymbolState(DEBUG_CSS flags, long argument) => S_OK;

        #endregion
        #region IDebugOutputCallbacks

        HRESULT IDebugOutputCallbacks.Output(DEBUG_OUTPUT mask, string text) =>
            ((IDebugOutputCallbacks2)this).Output(mask, text);

        HRESULT IDebugOutputCallbacks2.Output(DEBUG_OUTPUT mask, string text) =>
            throw new NotSupportedException($"This method is not used with {nameof(IDebugOutputCallbacks2)}. {nameof(IDebugOutputCallbacks2.Output2)} should be used instead.");

        HRESULT IDebugOutputCallbacks2.GetInterestMask(out DEBUG_OUTCBI mask)
        {
            //Receive output notifications for both DML and regular text
            mask = DEBUG_OUTCBI.ANY_FORMAT;
            return S_OK;
        }

        HRESULT IDebugOutputCallbacks2.Output2(DEBUG_OUTCB which, DEBUG_OUTCBF flags, long arg, string text)
        {
            HandleEvent(EngineOutput, new EngineOutputEventArgs(text));
            return S_OK;
        }

        #endregion

        private void HandleEvent<T>(EventHandler<T> handler, T args)
        {
            handler?.Invoke(this, args);
        }
    }
}
