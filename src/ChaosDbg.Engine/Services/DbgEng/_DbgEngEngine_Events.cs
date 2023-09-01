using System;
using ClrDebug;
using ClrDebug.DbgEng;
using static ChaosDbg.EventExtensions;
using static ClrDebug.HRESULT;

namespace ChaosDbg.DbgEng
{
    //Engine event handlers and IDebug* DbgEng callbacks

    partial class DbgEngEngine : IDebugEventCallbacks, IDebugOutputCallbacks2
    {
        #region ChaosDbg Event Handlers

        /// <summary>
        /// The event that occurs when the engine wishes to print output to the console.
        /// </summary>
        public event EventHandler<EngineOutputEventArgs> EngineOutput;

        /// <summary>
        /// The event that occurs when the debugger status changes (e.g. from broken to running).
        /// </summary>
        public event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged;

        /// <summary>
        /// The event that occurs when a module is loaded into the current process.
        /// </summary>
        public event EventHandler<EngineModuleLoadEventArgs> ModuleLoad;

        /// <summary>
        /// The event that occurs when a module is unloaded from the current process.
        /// </summary>
        public event EventHandler<EngineModuleUnloadEventArgs> ModuleUnload;

        #endregion
        #region IDebugEventCallbacks

        HRESULT IDebugEventCallbacks.GetInterestMask(out DEBUG_EVENT_TYPE mask)
        {
            mask =
                DEBUG_EVENT_TYPE.CHANGE_ENGINE_STATE | //Track when the debuggee state changes
                DEBUG_EVENT_TYPE.CREATE_PROCESS      | //We need to simulate a module load event when a process is created
                DEBUG_EVENT_TYPE.LOAD_MODULE         | //We want to know when modules are loaded into the target process
                DEBUG_EVENT_TYPE.UNLOAD_MODULE;        //We want to know when modules are unloaded from the target process

            return S_OK;
        }

        DEBUG_STATUS IDebugEventCallbacks.Breakpoint(IntPtr bp) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.Exception(ref EXCEPTION_RECORD64 exception, int firstChance) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.CreateThread(long handle, long dataOffset, long startOffset) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.ExitThread(int exitCode) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.CreateProcess(long imageFileHandle, long handle, long baseOffset, int moduleSize, string moduleName,
            string imageName, int checkSum, int timeDateStamp, long initialThreadHandle, long threadDataOffset,
            long startOffset)
        {
            //When WinDbg processes CreateProcess it simulates a call to NotifyLoadModuleEvent(), so we should do the same
            ((IDebugEventCallbacks)this).LoadModule(
                imageFileHandle: imageFileHandle,
                baseOffset: baseOffset,
                moduleSize: moduleSize,
                moduleName: moduleName,
                imageName: imageName,
                checkSum: checkSum,
                timeDateStamp: timeDateStamp
            );

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.ExitProcess(int exitCode) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.LoadModule(long imageFileHandle, long baseOffset, int moduleSize, string moduleName, string imageName, int checkSum, int timeDateStamp)
        {
            var module = Modules.Add(baseOffset, imageName, moduleName, moduleSize);

            HandleUIEvent(ModuleLoad, new EngineModuleLoadEventArgs(module));

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.UnloadModule(string imageBaseName, long baseOffset)
        {
            var module = Modules.Remove(baseOffset);

            if (module != null)
                HandleUIEvent(ModuleUnload, new EngineModuleUnloadEventArgs(module));

            return DEBUG_STATUS.NO_CHANGE;
        }

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
                        HandleUIEvent(EngineStatusChanged, new EngineStatusChangedEventArgs(oldStatus, newStatus));
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
            HandleUIEvent(EngineOutput, new EngineOutputEventArgs(text));
            return S_OK;
        }

        #endregion
    }
}
