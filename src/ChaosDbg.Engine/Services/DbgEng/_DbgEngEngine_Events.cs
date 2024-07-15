﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using ClrDebug;
using ClrDebug.DbgEng;
using static ChaosDbg.EventExtensions;
using static ClrDebug.DbgEng.DbgEngExtensions;
using static ClrDebug.HRESULT;

namespace ChaosDbg.DbgEng
{
    //Engine event handlers and IDebug* DbgEng callbacks

    partial class DbgEngEngine : IDebugEventCallbacks, IDebugOutputCallbacks2, IDebugInputCallbacks
    {
        #region ChaosDbg Event Handlers

        EventHandlerList IDbgEngineInternal.EventHandlers => EventHandlers;

        //These events are private so that external types cannot attempt to bind to them. All outside binding should go
        //through DebugEngineProvider. We can't pass explicitly implemented event handlers so HandleUIEvent, so annoyingly
        //we need to define "Raise" methods for all of them
        internal EventHandlerList EventHandlers { get; } = new EventHandlerList();

        private void RaiseEngineInitialized() =>
            HandleUIEvent((EventHandler<EngineInitializedEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineInitialized)], this, new EngineInitializedEventArgs(Session));

        private void RaiseEngineOutput(EngineOutputEventArgs args) =>
            HandleUIEvent((EventHandler<EngineOutputEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineOutput)], this, args);

        private void RaiseEngineStatusChanged(EngineStatusChangedEventArgs args) =>
            HandleUIEvent((EventHandler<EngineStatusChangedEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineStatusChanged)], this, args);

        private void RaiseEngineFailure(EngineFailureEventArgs args) =>
            HandleUIEvent((EventHandler<EngineFailureEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineFailure)], this, args);

        private void RaiseModuleLoad(EngineModuleLoadEventArgs args) =>
            HandleUIEvent((EventHandler<EngineModuleLoadEventArgs>) EventHandlers[nameof(DebugEngineProvider.ModuleLoad)], this, args);

        private void RaiseModuleUnload(EngineModuleUnloadEventArgs args) =>
            HandleUIEvent((EventHandler<EngineModuleUnloadEventArgs>) EventHandlers[nameof(DebugEngineProvider.ModuleUnload)], this, args);

        private void RaiseThreadCreate(EngineThreadCreateEventArgs args) =>
            HandleUIEvent((EventHandler<EngineThreadCreateEventArgs>) EventHandlers[nameof(DebugEngineProvider.ThreadCreate)], this, args);

        private void RaiseThreadExit(EngineThreadExitEventArgs args) =>
            HandleUIEvent((EventHandler<EngineThreadExitEventArgs>) EventHandlers[nameof(DebugEngineProvider.ThreadExit)], this, args);

        #endregion
        #region IDebugEventCallbacks

        HRESULT IDebugEventCallbacks.GetInterestMask(out DEBUG_EVENT_TYPE mask)
        {
            mask =
                DEBUG_EVENT_TYPE.CHANGE_ENGINE_STATE | //Track when the debuggee state changes
                DEBUG_EVENT_TYPE.CREATE_PROCESS      | //We need to simulate a module load event when a process is created
                DEBUG_EVENT_TYPE.CREATE_THREAD       |
                DEBUG_EVENT_TYPE.EXIT_THREAD         |
                DEBUG_EVENT_TYPE.LOAD_MODULE         | //We want to know when modules are loaded into the target process
                DEBUG_EVENT_TYPE.UNLOAD_MODULE;        //We want to know when modules are unloaded from the target process

            return S_OK;
        }

        DEBUG_STATUS IDebugEventCallbacks.Breakpoint(IntPtr bp) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.Exception(ref EXCEPTION_RECORD64 exception, int firstChance) => DEBUG_STATUS.NO_CHANGE;

        DEBUG_STATUS IDebugEventCallbacks.CreateThread(long handle, long dataOffset, long startOffset)
        {
            //The 3 parameters that are sent to this callback are the 3 fields included in a CREATE_THREAD_DEBUG_INFO struct.
            //dataOffset is in fact the address of the TEB

            Session.Processes.RefreshActiveProcess();

            GetEventThreadInfo(out var userId, out var systemId);

            Session.EventHistory.Add(new DbgEngThreadCreateEventHistoryItem(Session.ActiveProcess.Id, systemId));

            var thread = ActiveProcess.Threads.Add(userId, systemId, handle, dataOffset);
            
            RaiseThreadCreate(new EngineThreadCreateEventArgs(thread));

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.ExitThread(int exitCode)
        {
            Session.Processes.RefreshActiveProcess();

            GetEventThreadInfo(out _, out var systemId);

            Session.EventHistory.Add(new DbgEngThreadExitEventHistoryItem(Session.ActiveProcess.Id, systemId));

            var thread = ActiveProcess.Threads.Remove(systemId);

            if (thread != null)
                RaiseThreadExit(new EngineThreadExitEventArgs(thread));
            
            return DEBUG_STATUS.NO_CHANGE;
        }

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

        DEBUG_STATUS IDebugEventCallbacks.ExitProcess(int exitCode)
        {
            Session.Processes.RefreshActiveProcess();

            Session.EventHistory.Add(new DbgEngExitProcessEventHistoryItem(Session.ActiveProcess.Id));

            Session.Processes.Remove(ActiveProcess);

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.LoadModule(long imageFileHandle, long baseOffset, int moduleSize, string moduleName, string imageName, int checkSum, int timeDateStamp)
        {
            Session.Processes.RefreshActiveProcess();

            var module = ActiveProcess.Modules.Add(baseOffset, imageName, moduleName, moduleSize);

            Session.EventHistory.Add(new DbgEngModuleLoadEventHistoryItem(module));

            RaiseModuleLoad(new EngineModuleLoadEventArgs(module));

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.UnloadModule(string imageBaseName, long baseOffset)
        {
            Session.Processes.RefreshActiveProcess();

            var module = ActiveProcess.Modules.Remove(baseOffset);

            Session.EventHistory.Add(new DbgEngModuleUnloadEventHistoryItem(imageBaseName, baseOffset, ActiveProcess.Id));

            if (module != null)
                RaiseModuleUnload(new EngineModuleUnloadEventArgs(module));

            return DEBUG_STATUS.NO_CHANGE;
        }

        DEBUG_STATUS IDebugEventCallbacks.SystemError(int error, int level) => DEBUG_STATUS.NO_CHANGE;

        HRESULT IDebugEventCallbacks.SessionStatus(DEBUG_SESSION status) => S_OK;

        HRESULT IDebugEventCallbacks.ChangeDebuggeeState(DEBUG_CDS flags, long argument) => S_OK;

        #region ChangeEngineState

        HRESULT IDebugEventCallbacks.ChangeEngineState(DEBUG_CES flags, long argument)
        {
            Session.Processes.RefreshActiveProcess();

            if (flags.HasFlag(DEBUG_CES.EXECUTION_STATUS))
                ChangeEngineState_ExecutionStatus(argument);

            //I don't think there's much point trying to react to breakpoints being added;
            //it seems like the Offset of the IDebugBreakpoint may still be 0 when we get this
            //notification

            return S_OK;
        }

        private void ChangeEngineState_ExecutionStatus(long argument)
        {
            Debug.Assert(Session != null, "Session was null. Is a stale DebugClient attempting to reuse its event callbacks?");

            //The debuggee's execution status is changing (e.g. going from running to broken
            //into). However, sometimes DbgEng may be working on stuff internally that inadvertently
            //causes a ChangeEngineState() event to occur but we don't actually need to react to it yet.
            //When this is the case, the INSIDE_WAIT flag will be specified
            bool hasInsideWait = (argument & DEBUG_STATUS_FLAGS.INSIDE_WAIT) != 0;

            if (hasInsideWait == false)
            {
                //The first time this code path executes, we're being called from dbgeng!RawWaitForEvent -> PrepareForExecution,
                //which is called at the _start_ of RawWaitForEvent, before ::WaitForDebugEvent is called
                var oldStatus = Session.Status;

                //It's a real event that we need to be notified of
                var newStatus = ToEngineStatus((DEBUG_STATUS) argument);

                //Only react to the status if it's different than our current status
                if (oldStatus != newStatus)
                {
                    //If the user calls Execute() and steps or something, we're no longer broken, and if we try and wait afterwards,
                    //we need to know to now wait again
                    if (newStatus != EngineStatus.Break)
                        Session.BreakEvent.Reset();

                    //Something actually interesting has happened
                    Session.Status = newStatus;

                    //Notify any external subscribers (such as the UI) that the engine status ic changing
                    RaiseEngineStatusChanged(new EngineStatusChangedEventArgs(oldStatus, newStatus));
                }
            }
        }

        #endregion

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
            RaiseEngineOutput(new EngineOutputEventArgs(text));
            return S_OK;
        }

        #endregion
        #region IDebugInputCallbacks

        HRESULT IDebugInputCallbacks.StartInput(int bufferSize)
        {
            /* We need to call ReturnInput() to write something to g_InputBuffer and signal g_InputEvent
             * so that dbgeng!GetInput() may return. If we're a console application, we can easily do this right now.
             * But if we're a program with a UI, we may not have any command available. In this case, we need to signal
             * that we're now on the hunt for input to satisfy this StartInput() request. When we return, dbgeng!GetInput()
             * is going to hang waiting for g_InputEvent to be signalled. We'll raise a flag so that the next time we try
             * and execute a command, it will be sent to ReturnInput() rather than Execute() */

            Session.InputStarted = true;
            return S_OK;
        }

        HRESULT IDebugInputCallbacks.EndInput()
        {
            throw new NotImplementedException();
        }

        #endregion

        private EngineStatus ToEngineStatus(DEBUG_STATUS status)
        {
            switch (status)
            {
                //I guess just keep whatever the existing status is?
                case DEBUG_STATUS.NO_CHANGE:
                    return Session?.Status ?? EngineStatus.None;

                case DEBUG_STATUS.NO_DEBUGGEE:
                return EngineStatus.None;

            //I think GO_HANDLED and GO_NOT_HANDLED relate to exception handling. GO_HANDLED is also generated when EndSession() is called
                case DEBUG_STATUS.GO:
                case DEBUG_STATUS.REVERSE_GO:
                case DEBUG_STATUS.GO_HANDLED:

                //dbgeng!SetExecStepTrace will notify us of the fact we're planning to step
                case DEBUG_STATUS.STEP_OVER:
                case DEBUG_STATUS.STEP_INTO:
                case DEBUG_STATUS.REVERSE_STEP_OVER:
                case DEBUG_STATUS.REVERSE_STEP_INTO:
                    return EngineStatus.Continue;

                case DEBUG_STATUS.BREAK:
                    return EngineStatus.Break;
            }

            throw new UnknownEnumValueException(status);
        }
    }
}
