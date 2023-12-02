﻿using System;
using ClrDebug;
using static ChaosDbg.EventExtensions;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void RegisterCallbacks(CordbManagedCallback cb)
        {
            cb.OnPreEvent += PreEvent;

            cb.OnCreateProcess += CreateProcess;
            cb.OnLoadModule += LoadModule;
            cb.OnUnloadModule += UnloadModule;
            cb.OnCreateThread += CreateThread;
            cb.OnExitThread += ExitThread;

            cb.OnAnyEvent += AnyEvent;
        }

        #region ChaosDbg Event Handlers

#pragma warning disable CS0067 //Event is never used
        public event EventHandler<EngineOutputEventArgs> EngineOutput;
        public event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged;
        public event EventHandler<EngineModuleLoadEventArgs> ModuleLoad;
        public event EventHandler<EngineModuleUnloadEventArgs> ModuleUnload;
        public event EventHandler<EngineThreadCreateEventArgs> ThreadCreate;
        public event EventHandler<EngineThreadExitEventArgs> ThreadExit;
#pragma warning restore CS0067 //Event is never used

        #endregion
        #region PreEvent

        private void PreEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            UpdateCurrentThread(e);
        }

        private void UpdateCurrentThread(CorDebugManagedCallbackEventArgs e)
        {
            //The following is a list of event types that contain a CorDebugThread member.
            //If we see one of these, update the last seen thread

            if (e is AppDomainThreadDebugCallbackEventArgs a)
                Target.ActiveThread = a.Thread;
            else if (e is DataBreakpointCorDebugManagedCallbackEventArgs d)
                Target.ActiveThread = d.Thread;
            if (e is MDANotificationCorDebugManagedCallbackEventArgs m)
                Target.ActiveThread = m.Thread;
        }

        #endregion
        #region Process

        private void CreateProcess(object sender, CreateProcessCorDebugManagedCallbackEventArgs e)
        {
            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process
            e.Process.TrySetDesiredNGENCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);
        }

        #endregion
        #region Module

        private void LoadModule(object sender, LoadModuleCorDebugManagedCallbackEventArgs e)
        {
            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process
            e.Module.TrySetJITCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);

            var module = ActiveProcess.Modules.Add(e.Module);

            HandleUIEvent(ModuleLoad, new EngineModuleLoadEventArgs(module));
        }

        private void UnloadModule(object sender, UnloadModuleCorDebugManagedCallbackEventArgs e)
        {
            var module = ActiveProcess.Modules.Remove(e.Module.BaseAddress);

            if (module != null)
                HandleUIEvent(ModuleUnload, new EngineModuleUnloadEventArgs(module));
        }

        #endregion
        #region Thread

        private void CreateThread(object sender, CreateThreadCorDebugManagedCallbackEventArgs e)
        {
            var thread = ActiveProcess.Threads.Add(e.Thread);

            HandleUIEvent(ThreadCreate, new EngineThreadCreateEventArgs(thread));
        }

        private void ExitThread(object sender, ExitThreadCorDebugManagedCallbackEventArgs e)
        {
            var thread = ActiveProcess.Threads.Remove(e.Thread.Id);

            if (thread != null)
                HandleUIEvent(ThreadExit, new EngineThreadExitEventArgs(thread));
        }

        #endregion

        private void AnyEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            //Even when we call Stop() to break into the process, if we're in the middle of processing an event, we'll end up calling
            //Continue() again here.
            if (Target.Process.HasQueuedCallbacks)
            {
                DoContinue(e.Controller);
                return;
            }

            //The Any event is processed last. If any of our other event handlers objected
            //to continuing, the will have set Continue to false
            if (e.Continue)
            {
                DoContinue(e.Controller);
            }
            else
            {
                OnStopping();
            }
        }

        private void OnStopping()
        {
            //We're not continuing. Update debugger state
            Target.Process.DAC.Threads.Refresh();
        }

        private void SetEngineStatus(EngineStatus newStatus)
        {
            var oldStatus = Target.Status;

            if (oldStatus != newStatus)
            {
                Target.Status = newStatus;

                HandleUIEvent(EngineStatusChanged, new EngineStatusChangedEventArgs(oldStatus, newStatus));
            }
        }
    }
}
