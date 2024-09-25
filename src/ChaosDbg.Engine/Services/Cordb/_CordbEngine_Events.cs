using System;
using System.ComponentModel;
using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        internal void RegisterCallbacks(CordbManagedCallback cb)
        {
            cb.OnEngineFailure = CriticalFailure;

            cb.OnPreEvent += PreManagedEvent;

            cb.OnCreateProcess += CreateProcess;
            cb.OnExitProcess += ExitProcess;

            cb.OnDebuggerError += DebuggerError;

            cb.OnCreateAppDomain += CreateAppDomain;
            cb.OnExitAppDomain += ExitAppDomain;

            cb.OnLoadAssembly += LoadAssembly;
            cb.OnUnloadAssembly += UnloadAssembly;

            cb.OnLoadModule += LoadModule;
            cb.OnUnloadModule += UnloadModule;

            cb.OnCreateThread += CreateThread;
            cb.OnExitThread += ExitThread;

            cb.OnBreakpoint += Breakpoint;
            cb.OnDataBreakpoint += DataBreakpoint;
            cb.OnBreakpointSetError += BreakpointSetError;

            cb.OnNameChange += NameChange;

            cb.OnAnyEvent += AnyManagedEvent;
        }

        internal void RegisterUnmanagedCallbacks(CordbUnmanagedCallback ucb)
        {
            if (ucb == null)
                return;

            ucb.OnEngineFailure = CriticalFailure;

            ucb.OnPreEvent += PreUnmanagedEvent;

            ucb.OnAnyEvent += AnyUnmanagedEvent;
            ucb.OnCreateProcess += UnmanagedCreateProcess;
            ucb.OnExitProcess += UnmanagedExitProcess;
            ucb.OnLoadDll += UnmanagedLoadModule;
            ucb.OnUnloadDll += UnmanagedUnloadModule;
            ucb.OnCreateThread += UnmanagedCreateThread;
            ucb.OnExitThread += UnmanagedExitThread;
            ucb.OnException += UnmanagedException;
        }

        #region ChaosDbg Event Handlers

        EventHandlerList IDbgEngineInternal.EventHandlers => EventHandlers;

        internal EventHandlerList EventHandlers { get; } = new EventHandlerList();

        private void RaiseEngineInitialized() =>
            services.UserInterface.HandleEvent((EventHandler<EngineInitializedEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineInitialized)], this, new EngineInitializedEventArgs(Session));

        private void RaiseEngineOutput(EngineOutputEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineOutputEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineOutput)], this, args);

        private void RaiseEngineStatusChanged(EngineStatusChangedEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineStatusChangedEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineStatusChanged)], this, args);

        private void RaiseEngineFailure(EngineFailureEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineFailureEventArgs>) EventHandlers[nameof(DebugEngineProvider.EngineFailure)], this, args);

        private void RaiseModuleLoad(EngineModuleLoadEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineModuleLoadEventArgs>) EventHandlers[nameof(DebugEngineProvider.ModuleLoad)], this, args);

        private void RaiseModuleUnload(EngineModuleUnloadEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineModuleUnloadEventArgs>) EventHandlers[nameof(DebugEngineProvider.ModuleUnload)], this, args);

        private void RaiseThreadCreate(EngineThreadCreateEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineThreadCreateEventArgs>) EventHandlers[nameof(DebugEngineProvider.ThreadCreate)], this, args);

        private void RaiseThreadExit(EngineThreadExitEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineThreadExitEventArgs>) EventHandlers[nameof(DebugEngineProvider.ThreadExit)], this, args);

        private void RaiseBreakpointHit(EngineBreakpointHitEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineBreakpointHitEventArgs>) EventHandlers[nameof(DebugEngineProvider.BreakpointHit)], this, args);

        private void RaiseExceptionHit(EngineExceptionHitEventArgs args) =>
            services.UserInterface.HandleEvent((EventHandler<EngineExceptionHitEventArgs>) EventHandlers[nameof(DebugEngineProvider.ExceptionHit)], this, args);

        #endregion
        #region PreEvent

        private void PreManagedEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            PreEventCommon();

            Session.CallbackContext.ClearManaged();
        }

        private void PreUnmanagedEvent(object sender, DebugEventCorDebugUnmanagedCallbackEventArgs e)
        {
            //Always do this first so that the callback context is overwritten
            PreEventCommon();

            Session.CallbackContext.ClearUnmanaged();

            //Store some information about who the event pertains to
            Session.CallbackContext.UnmanagedEventProcessId = e.DebugEvent.dwProcessId;
            Session.CallbackContext.UnmanagedEventThreadId = e.DebugEvent.dwThreadId;
            Session.CallbackContext.UnmanagedOutOfBand = e.OutOfBand;
            Session.CallbackContext.UnmanagedContinue = true;
            Session.CallbackContext.UnmanagedEventType = e.DebugEvent.dwDebugEventCode;
        }

        private void PreEventCommon()
        {
            var localStartupFailed = Session.StartupFailed;

            if (!localStartupFailed)
            {
                var process = Session.ActiveProcess;

                Debug.Assert(process != null, $"{nameof(PreEventCommon)}: Didn't have a process! This indicates the process was not properly stored prior to the debuggee being resumed");
            }

            Session.CallbackStopCount++;
        }

        #endregion
        #region Process

        private void CreateProcess(object sender, CreateProcessCorDebugManagedCallbackEventArgs e)
        {
            /* The CLR will only generate a LoadModule event for a process when it has a purely managed entry point.
             * If a process hosts the CLR, we won't get a managed LoadModule event. Thus, powershell.exe does not
             * generate a LoadModule event, while powershell_ise.exe does. Thus, if we want to perform any type of
             * analytics against a partially managed process module, we need to be interop debugging. This is the
             * behavior of dnSpy, mdbg and Visual Studio. No, there isn't a CorDebugModule hanging off the
             * CorDebugProcess for us to extract either. */

            //Our thread logger context is initialized in CordbLauncher

            /* When attaching to a process, assuming that the attach isn't happening so early in the process' lifetime that a
             * real CreateProcess event hasn't even been fired yet, ShimProcess::QueueFakeAttachEvents will generate a number of "fake"
             * debug events in order to bring us up to speed on the status of the AppDomain, Assembly, Module and Thread objects in
             * the target process. */

            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process.
            //This will fail when attempting to attach
            e.Process.TrySetDesiredNGENCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);

            var module = Process.Modules.Add(e.Process);

            if (!Session.IsInterop)
            {
                //Add the history so that we can see that we've had a managed event and will know that the CLR has been loaded
                Session.EventHistory.Add(CordbManagedEventHistoryItem.New(e));

                //Let's kick off a load for CLR symbols now so that they're ready for when we need them
                Process.Symbols.LoadCLRSymbolsForNonInterop();
            }

            RaiseModuleLoad(new EngineModuleLoadEventArgs(module));
        }

        private void ExitProcess(object sender, ExitProcessCorDebugManagedCallbackEventArgs e)
        {
            //Don't have a Process if startup failed
            if (!Session.StartupFailed)
            {
                var module = Process.Modules.Remove(e.Process);

                if (module != null)
                    RaiseModuleUnload(new EngineModuleUnloadEventArgs(module));
            }

            /* On Windows, a process has not truly exited until we receive the EXIT_PROCESS_DEBUG_EVENT. My observation has been that, when interop debugging,
             * we always receive EXIT_PROCESS_DEBUG_EVENT prior to receiving the managed ExitProcess event. While I don't know whether a process handle
             * being signalled because it exited necessarily proves that EXIT_PROCESS_DEBUG_EVENT has also been fired. At this stage I don't see any
             * reason why this would even matter.
             *
             * In any case, as far as mscordbi is concerned, even if the CLR hasn't been loaded, and we never received a managed CreateProcess event,
             * we'll still receive a managed ExitProcess event, because a ExitProcessWorkItem is specially queued onto the CordbRCEventThread.
             * In rare scenarios, its possible for */

            //Signal the wait as completed
            Session.IsTerminated = true;
            Log.Debug<CordbEngine>("Setting WaitExitProcess from ExitProcess");
            Session.WaitExitProcess.Set();
        }

        private void UnmanagedCreateProcess(object sender, CREATE_PROCESS_DEBUG_INFO e)
        {
            /* Events are dispatched to us from a Win32 Event Thread. CordbWin32EventThread::Start() creates
             * a new thread that executes CordbWin32EventThread::ThreadProc() -> Win32EventLoop(). Win32EventLoop()
             * loops around calling ::WaitForDebugEvent. When it gets an event it calls ShimProcess::HandleWin32DebugEvent()
             * which in turn dispatches to CordbProcess::HandleDebugEventForInteropDebugging().
             * Our thread name is set inside CordbLauncher. */

            //Our thread logger context is initialized in CordbLauncher

            /* When attaching, NtDebugActiveProcess will call DbgkpPostFakeProcessCreateMessages to send us a bunch of fake
             * CREATE_PROCESS_DEBUG_EVENT, CREATE_THREAD_DEBUG_EVENT and LOAD_DLL_DEBUG_EVENT messages, immediately bringing
             * us up to speed */

            /* CordbWin32EventThread::ThreadProc() very annoyingly calls DebugSetProcessKillOnExit(FALSE)
             * which prevents the debuggee from terminating when the parent debugger terminates. This is very annoying when
             * interop debugging, where you'll likely have stopped at a breakpoint. If you terminate the process before
             * continuing, even though you may have removed the breakpoint, the breakpoint _event_ was not marked as handled
             * (via ContinueDebugEvent) and so the process then crashes saying there was an unhandled exception. mscordbi doesn't
             * even support _detaching_ from a target process, so it makes no sense that we shouldn't kill the debuggee when the
             * debugger terminates */
            Kernel32.DebugSetProcessKillOnExit(true);

            Session.EventHistory.Add(CordbNativeEventHistoryItem.CreateProcess());
            Session.EventHistory.Add(CordbNativeEventHistoryItem.CreateThread(Session.CallbackContext.UnmanagedEventThreadId));

            /* Register the thread for the main thread. There won't be a separate CREATE_THREAD_DEBUG_EVENT event for this.
             * When interop debugging, due to the fact you have to let the process start running before you can actually attach
             * to it, there will already be like 5 other threads running by the time this CREATE_PROCESS_DEBUG_EVENT is dispatched.
             * Not to worry, CREATE_THREAD_DEBUG_EVENT notifications for those threads will soon be incoming. */
            Process.Threads.Add(Session.CallbackContext.UnmanagedEventThreadId, e.hThread);

            //Ensure the process gets registered as a module too. This will also add the module event history item
            UnmanagedLoadModule(sender, new LOAD_DLL_DEBUG_INFO
            {
                hFile = e.hFile, //We'll close hFile in CordbUnmanagedCallback.DebugEvent()
                lpBaseOfDll = e.lpBaseOfImage,
                dwDebugInfoFileOffset = e.dwDebugInfoFileOffset,
                nDebugInfoSize = e.nDebugInfoSize,
                lpImageName = e.lpImageName,
                fUnicode = e.fUnicode
            });

            //Flag this module as being the primary module of a process, so we know what module to remove
            //when we hit our UnmanagedExitProcess event
            Process.Modules.SetAsProcess(e.lpBaseOfImage, Session.CallbackContext.UnmanagedEventProcessId);
        }

        private void UnmanagedExitProcess(object sender, EXIT_PROCESS_DEBUG_INFO e)
        {
#if DEBUG
            Session.IsPerformingExitProcess = true;
#endif

            //If there's an issue with DbgHelp which causes us to hang, we want to record this fact, as us not returning will block the managed ExitProcess being fired
            Log.Debug<CordbEngine>("UnmanagedExitProcess: attempting to remove process module...");

            Process.Modules.RemoveProcessModule(Session.CallbackContext.UnmanagedEventProcessId);

            Log.Debug<CordbEngine>("UnmanagedExitProcess: finished removing process module");

#if DEBUG
            Session.IsPerformingExitProcess = false;
#endif
        }

        #endregion
        #region DebuggerError

        private void DebuggerError(object sender, DebuggerErrorCorDebugManagedCallbackEventArgs e)
        {
            /* DebuggerError is exclusively called when mscordbi detects an unrecoverable error in the debugger engine.
             * However, due to races that can occur during process termination, not all DebuggerError events are necessarily "issues"
             * (See the notes in the Shutdown region of CordbEngine.cs for more information)
             *
             * Thus, we must attempt to make sense of any errors that we receive. */

            //There's no continuing from an unrecoverable error (it'll just throw that there was an unrecoverable error)
            e.Continue = false;

            switch (e.ErrorHR)
            {
                case HRESULT.CORDBG_E_PROCESS_TERMINATED:
                    //If we're expecting this event, all good (see CordbEngine.cs for info on when we may receive a DebuggerError event
                    //instead of an ExitProcess event during shutdown)
                    Session.IsTerminated = true;
                    
                    if (Session.IsTerminating)
                    {
                        /* We've now got an issue. The Win32 Event Thread is normally stopped in ShimProcess::Dispose(), which is called
                         * from CordbProcess::Neuter(), which is itself exclusively called from ExitProcessWorkItem::Do(). But, as described in
                         * CordbEngine.cs, the whole issue here is that, as a result of a race, the process was terminated while the fake attach events
                         * were being calculated, prior to the event that would've been processed _before_ the ExitProcessWorkItem event. */

                        Log.Debug<CordbEngine>("Setting WaitExitProcess from DebuggerError");

                        Log.Warning<CordbEngine>("ShimProcess and Win32 Event Thread have been leaked");

                        Debug.Assert(false, "ShimProcess and Win32 Event Thread have been leaked. If we attached to this process, we should try and ensure we only shutdown after we've received the initial fake attach events");

                        Session.WaitExitProcess.Set();
                    }
                    else
                        RaiseEngineFailure(new EngineFailureEventArgs(new DebugException(e.ErrorHR), EngineFailureStatus.BeginShutdown)); //Process unexpectedly shutdown! The user needs to know that their process was ripped out from under them!

                    break;

                default:
                    //Don't know what happened, but it's not good!
                    RaiseEngineFailure(new EngineFailureEventArgs(new DebugException(e.ErrorHR), EngineFailureStatus.BeginShutdown));
                    break;
            }
        }

        #endregion
        #region AppDomain

        private void CreateAppDomain(object sender, CreateAppDomainCorDebugManagedCallbackEventArgs e)
        {
            Process.AppDomains.Add(e.AppDomain);
        }

        private void ExitAppDomain(object sender, ExitAppDomainCorDebugManagedCallbackEventArgs e)
        {
            //Will automatically unlink itself from any assemblies as well
            Process.AppDomains.Remove(e.AppDomain);
        }

        #endregion
        #region Assembly

        private void LoadAssembly(object sender, LoadAssemblyCorDebugManagedCallbackEventArgs e)
        {
            //Will automatically link itself to its AppDomain as well
            Process.Assemblies.Add(e.Assembly);
        }

        private void UnloadAssembly(object sender, UnloadAssemblyCorDebugManagedCallbackEventArgs e)
        {
            //Will automatically unlink itself from any AppDomains/modules as well
            Process.Assemblies.Remove(e.Assembly);
        }

        #endregion
        #region Module

        private void LoadModule(object sender, LoadModuleCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient

            //Will automatically link itself to its Assembly as well
            var module = Process.Modules.Add(e.Module);

            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process
            var hr = e.Module.TrySetJITCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);

            RaiseModuleLoad(new EngineModuleLoadEventArgs(module));
        }

        private void UnloadModule(object sender, UnloadModuleCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient
            var module = Process.Modules.Remove(e.Module.BaseAddress);

            if (module != null)
                RaiseModuleUnload(new EngineModuleUnloadEventArgs(module));
        }

        private void UnmanagedLoadModule(object sender, LOAD_DLL_DEBUG_INFO e)
        {
            var module = Process.Modules.Add(e);

            //We'll close hFile in CordbUnmanagedCallback.DebugEvent()

            Session.EventHistory.Add(new CordbNativeModuleLoadEventHistoryItem(module));
            RaiseModuleLoad(new EngineModuleLoadEventArgs(module, Session.CallbackContext.UnmanagedOutOfBand));
        }

        private void UnmanagedUnloadModule(object sender, UNLOAD_DLL_DEBUG_INFO e)
        {
            //At the point we receive this notification, the module has already been unloaded from the target process.
            //If we're in the middle of trying to read the PE Headers of the target module, we will fail and throw an exception
            var module = Process.Modules.Remove(e);

            if (module != null)
            {
                Session.EventHistory.Add(new CordbNativeModuleUnloadEventHistoryItem(module));
                RaiseModuleUnload(new EngineModuleUnloadEventArgs(module, Session.CallbackContext.UnmanagedOutOfBand));
            }
        }

        #endregion
        #region Thread

        private void CreateThread(object sender, CreateThreadCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient

            /* Will we receive managed CreateThread notifications when a thread is initially started outside
             * of managed code? Yes! There are three scenarios under which native code could call into the runtime:
             *
             * 1. In .NET Framework a DLL Export is called. The loader will automatically invoke the DLLs entry point,
             *    which will result in _CorDllMain() being invoked in mscoree. This will relay to mscoreei!_CorDllMain ->
             *    clr!_CorDllMain -> clr!ExecuteDLL. From here, it will either call clr!EnsureEEStarted -> clr!EEStartup ->
             *    clr!EEStartupHelper -> clr!SetupThread, or if the CLR was already initialized in another thread will call
             *    clr!SetupThreadNoThrow
             *
             * 2. In .NET Core, if a function pointer is passed to native code wrapping a managed method decorated with
             *    UnmanagedCallersOnlyAttribute, a stub would be created that encapsulates the managed method. When this
             *    stub is invoked, it will take you to clr!PreStubWorker -> clr!PreStubWorker_Preemptive which then calls
             *    CREATETHREAD_IF_NULL_FAILFAST which invokes SetupThreadNoThrow(). Note that UnmanagedCallersOnlyAttribute
             *    can only be used for creating "real" exports in NativeAOT. DNNE works around this by writing a C++ file
             *    that relays from a "real" export to a delegate of the UnmanagedCallersOnlyAttribute decorated method retrieved
             *    from HostFxr
             *
             * 3. A method on a COM interface is invoked. By default, the COM method's vtable slot points to clr!ComCallPreStub.
             *    This function dispatches to clr!ComPreStubWorker which calls SetupThreadNoThrow(). After the "real" method
             *    has been generated, the VTable is patched so that the next method call will go to clr!GenericComCallStub instead. */
            if (Session.IsInterop)
            {
                var thread = Process.Threads[e.Thread.Id];

                Debug.Assert(thread != null);

                //Upgrade the thread to be managed!
                thread.Accessor = new CordbThread.ManagedAccessor(e.Thread);
                Process.Threads.IdentifySpecialThreads(thread);
            }
            else
            {
                //When not interop debugging, we're responsible for creating the new thread

                var thread = Process.Threads.Add(e.Thread);

                RaiseThreadCreate(new EngineThreadCreateEventArgs(thread));
            }
        }

        private void ExitThread(object sender, ExitThreadCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient

            //In interop debugging, it's the unmanaged event handler's responsibility to cleanup the thread
            if (!Session.IsInterop)
            {
                var thread = Process.Threads.Remove(e.Thread.Id);

                if (thread != null)
                    RaiseThreadExit(new EngineThreadExitEventArgs(thread));
            }
        }

        private void NameChange(object sender, NameChangeCorDebugManagedCallbackEventArgs e)
        {
            //Either an AppDomain or Thread name changed

            if (e.Thread != null)
            {
                /* It was a thread name change. As per the documentation on Debugger::NameChangeEvent(),
                 * in order to an AppDomain's name change, just do AppDomain.Name. But in order to get
                 * a Thread's name change, you're recommended to do an eval of Thread::get_Name. Note that
                 * since this information is stored in a field, you can do a simple query against an ICorDebugValue
                 * to get this information. When you call Thread.Name = "value", it dispatches
                 * to ThreadNative::InformThreadNameChange(). This method does 3 things in the following order:
                 *
                 * 1. Calls SetThreadDescriptor()
                 * 2. Notifies the profiler via ICorProfilerCallback2::ThreadNameChanged()
                 * 3. Calls Debugger::NameChangeEvent().
                 *
                 * But there's a catch: because Get/SetThreadDescriptor were only introduced in
                 * Windows 10 1607, they aren't part of .NET Framework's version of ThreadNative::InformThreadNameChange()!
                 * As such, your only true option is to use func-eval. */

                var cordbThread = Process.Threads[e.Thread.Id];
                ((CordbThread.ManagedAccessor) cordbThread.Accessor).RefreshName();
            }
        }

        private void UnmanagedCreateThread(object sender, CREATE_THREAD_DEBUG_INFO e)
        {
            //Don't log event history here; the default handler is sufficient

            var thread = Process.Threads.Add(Session.CallbackContext.UnmanagedEventThreadId, e.hThread);

            RaiseThreadCreate(new EngineThreadCreateEventArgs(thread, Session.CallbackContext.UnmanagedOutOfBand));
        }

        private void UnmanagedExitThread(object sender, EXIT_THREAD_DEBUG_INFO e)
        {
            //Don't log event history here; the default handler is sufficient

            var thread = Process.Threads.Remove(Session.CallbackContext.UnmanagedEventThreadId);

            if (thread != null)
                RaiseThreadExit(new EngineThreadExitEventArgs(thread, Session.CallbackContext.UnmanagedOutOfBand));
            else
                Log.Debug<CordbEngine>("Received ExitThread notification for unknown thread {tid}", Session.CallbackContext.UnmanagedEventThreadId);
        }

        #endregion
        #region Breakpoint / Exception

        private void Breakpoint(object sender, BreakpointCorDebugManagedCallbackEventArgs e)
        {
            //Change the active thread to whoever encountered this exception
            Process.Threads.SetActiveThread(e.Thread);

            //Ensure that we don't automatically continue, and add a reason why not
            AddManagedPause(e, new CordbManagedBreakpointEventPauseReason());

            var breakpoint = Process.Breakpoints.GetBreakpoint(e.Breakpoint);

            RaiseBreakpointHit(new EngineBreakpointHitEventArgs(breakpoint));
        }

        private void DataBreakpoint(object sender, DataBreakpointCorDebugManagedCallbackEventArgs e)
        {
            //Change the active thread to whoever encountered this exception
            Process.Threads.SetActiveThread(e.Thread);

            throw new NotImplementedException();
        }

        private void BreakpointSetError(object sender, BreakpointSetErrorCorDebugManagedCallbackEventArgs e)
        {
            throw new NotImplementedException();
        }

        private unsafe void UnmanagedException(object sender, EXCEPTION_DEBUG_INFO e)
        {
            //Change the active thread to whoever encountered this exception
            Process.Threads.SetActiveThread(Session.CallbackContext.UnmanagedEventThreadId);

            /* Whenever we hit an unmanaged breakpoint, we need to clear the 0xcc byte so that we can execute the normal
             * instruction. But this then creates a problem: we need to re-add that breakpoint so that we hit it next time!
             * We solve this by "deferring" the reinsertion of the breakpoint until we hit the _next_ instruction, meaning
             * we'll need to insert a sneaky step on that instruction so that we can reinsert the breakpoint. If it just so
             * happens that there's already a "normal" breakpoint on the next instruction, even better: we'll just rely on that
             * instead. This logic will also catch the user doing a single step - a step is just a regular breakpoint (just temporary) */
            Process.Breakpoints.ProcessDeferredBreakpoint();

            //See the comments in CordbBreakpoint.cs for information on how code breakpoints work in the CLR

            /* How does DbgEng handle exceptions?
             * LiveUserTargetInfo::ProcessDebugEvent
             * LiveUserTargetInfo::ProcessEventException
             * ProcessBreakpointOrStepException
             *     if its STATUS_BREAKPOINT its a breakpoint
             * CheckBreakpointOrStepTrace
             * CheckBreakpointHit
             * RemoveBreakpoints
             *
             * CodeBreakpoint::Remove
             * LiveUserTargetInfo::RemoveCodeBreakpoint
             *
             * Amd64MachineInfo::InsertThreadDataBreakpoints ?
             */

            if (!e.dwFirstChance)
                throw new NotImplementedException("Handling second chanve exceptions is not implemented");

            //You MUST call CorDebugProcess.ClearCurrentException() for any breakpoints you handle. Otherwise, any subsequent actions you try and perform (such as stepping)
            //will be ignored
            switch (e.ExceptionRecord.ExceptionCode)
            {
                case NTSTATUS.STATUS_BREAKPOINT:
                    UnmanagedStatusBreakpoint(e);
                    break;

                case NTSTATUS.STATUS_SINGLE_STEP:
                    UnmanagedSingleStep(e);
                    break;

                case NTSTATUS.STATUS_CPP_EH_EXCEPTION:
                case NTSTATUS.STATUS_ACCESS_VIOLATION:
                    ProcessAppException(e);
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle an exception with code {e.ExceptionRecord.ExceptionCode}");
            }
        }

        private unsafe void ProcessStatusBreakpoint(in EXCEPTION_DEBUG_INFO exception)
        {
            /* When debugging .NET Framework, the first breakpoint we receive will be the loader breakpoint, because
             * we call ICorDebug.CreateProcess() using DEBUG_ONLY_THIS_PROCESS. However, when debugging .NET Core,
             * we first create the process standalone, hook the CLR startup event, and then call ICorDebug.DebugActiveProcess,
             * which also appears to result in a breakpoint being triggered (https://learn.microsoft.com/en-us/windows/win32/api/debugapi/nf-debugapi-debugactiveprocess)
             */

            /* First thing's first, if we just hit a 0xcc, then the IP is now pointing to the instruction after it.
             * If this breakpoint belongs to us, we need to rewind so that we can display (and then execute) the original instruction
             * upon resuming. If this breakpoint doesn't belong to us, we need to display the fact that the int3, and not the instruction
             * after it, is the current instruction. Either way, we need to rewind our IP by 1.
             *
             * When the debuggee is resumed, if the breakpoint did belong to us, we'll have replaced it with the original instruction, which
             * we will now execute. If the breakpoint did not belong to us, we need to reverse the decrement we did on the IP (which, remember,
             * we only did for display purposes) so that we resume execution from the instruction after the breakpoint*/
            var thread = Session.CallbackContext.UnmanagedEventThread;
            thread.RegisterContext.IP--;

            if (!Session.HaveLoaderBreakpoint)
            {
                //On attach, the "loader breakpoint" will be the "attach breakpoint" described under https://learn.microsoft.com/en-us/windows/win32/api/debugapi/nf-debugapi-debugactiveprocess

                Session.HaveLoaderBreakpoint = true;

                //Set CUES_ExceptionCleared so that mscordbi!DoDbgContinue() passes DBG_CONTINUE to ContinueDebugEvent()
                Process.CorDebugProcess.ClearCurrentException(thread.Id);

                //Add the pause before raising the event
                AddUnmanagedPause(new CordbNativeBreakpointEventPauseReason(Session.CallbackContext.UnmanagedOutOfBand));
                RaiseBreakpointHit(new EngineBreakpointHitEventArgs(new CordbSpecialBreakpoint(CordbSpecialBreakpointKind.Loader, exception)));
                return;
            }

            if (Process.Breakpoints.TryGetBreakpoint(exception.ExceptionRecord.ExceptionAddress, out var breakpoint))
            {
                if (breakpoint.IsOneShot)
                {
                    Process.Breakpoints.Remove(breakpoint);
                }
                else
                {
                    //Temporarily suspend this breakpoint so that we can execute its normal CPU instruction
                    breakpoint.SetSuspended(true);
                }

                /* DbgEng seems to play funny games with globally storing its context, reusing it any time anyone asks a context related question (e.g. as part of a stack trace
                 * or showing register values), only commiting its changes when the debugger resumes execution. We don't want to have messy global state. As such, we're forced
                /* to commit our changes we've made immediately */
                thread.TrySaveRegisterContext();

                //Set CUES_ExceptionCleared so that mscordbi!DoDbgContinue() passes DBG_CONTINUE (DBG_FORCE_CONTINUE in Longhorn) to ContinueDebugEvent()
                Process.CorDebugProcess.ClearCurrentException(thread.Id);

                //Add the pause before raising the event
                AddUnmanagedPause(new CordbNativeBreakpointEventPauseReason(Session.CallbackContext.UnmanagedOutOfBand));
                RaiseBreakpointHit(new EngineBreakpointHitEventArgs(breakpoint));

                //It's important that we do this _after_ we record the pause state above. We'll now lie and say that
                //the event wasn't an OOB event so that we don't automatically continue. When the user resumes, we'll
                //use the fact that we paused during an OOB event (as recorded in the pause reason) to do an OOB continue
                if (breakpoint is CordbRawCodeBreakpoint)
                    Session.CallbackContext.UnmanagedOutOfBand = false;
            }
            else
            {
                //It's an unhandled exception then!

                Process.Symbols.TryGetSymbolFromAddress((long) (void*) exception.ExceptionRecord.ExceptionAddress, out var unhandledTarget);

                throw new NotImplementedException("Processing unhandled exceptions is not implemented");
            }
        }

        private void UnmanagedSingleStep(in EXCEPTION_DEBUG_INFO exception)
        {
            var tid = Session.CallbackContext.UnmanagedEventThreadId;

            //Set CUES_ExceptionCleared so that mscordbi!DoDbgContinue() passes DBG_CONTINUE (DBG_FORCE_CONTINUE in Longhorn) to ContinueDebugEvent()
            Process.CorDebugProcess.ClearCurrentException(tid);

            AddUnmanagedPause(new CordbNativeStepEventPauseReason(Session.CallbackContext.UnmanagedOutOfBand, exception));
            RaiseBreakpointHit(new EngineBreakpointHitEventArgs(null)); //temp
        }

        #endregion

        private void CriticalFailure(Exception ex, EngineFailureStatus status)
        {
            Log.Error<CordbEngine>(ex, "A critical failure occurred: {message}. ChaosDbg will now shutdown", ex.Message);

            Debug.Assert(Session != null, "An event callback thread was allowed to run before the session was initialized, or after the session was cleared");

            //When we crash during CordbLauncher, we won't have a Process yet, but we will have already stored the process to use ourselves
            if (!Session.IsCrashing)
                Session.SetCrashingProcess(Process.CorDebugProcess);

            RaiseEngineFailure(new EngineFailureEventArgs(ex, status));

            Session.EventHistory.Add(new CordbEngineFailureEventHistoryItem(ex, status));

            //We can't request termination from here, as we're running on a callback thread. We can't trust that any of our other engine threads haven't exploded,
            //so just spin up a new one and hope for the best

            Session.InitializeCriticalFailureThread(() =>
            {
                /* Disposing the session will cause the engine cancellation token to be cancelled, which will cause the ChaosDbg engine thread
                 * to stop and terminate the process. As this method also waits for the ChaosDbg engine thread to end before returning, we have to call
                 * this in another thread so that we don't block the callback thread while waiting for this to return. If the ChaosDbg engine thread has
                 * already terminated, then the ICorDebugProcess will have already been cleaned up, so either way we know everything will have been cleaned up */
                try
                {
                    if (!Session.StartupFailed)
                    {
                        //If startup failed, leave it to CordbLauncher to cleanup everything

                        //Depending on the code path that lead here, CorDebug may not have been terminated yet. Thus, we need to ensure that we terminate it so that it's already null when we call CordbSessionInfo.Dispose()
                        StopAndTerminate();

                        Session.Dispose();
                    }

                    /* If we get to this point, this means the ChaosDbg engine thread has ended. And when the ChaosDbg engine thread ends,
                     * it stops and terminates our CordbProcess, which itself waits for the WaitExitProcess event. Every single aspect of ICorDebug
                     * has now been cleaned up. Success! */
                    RaiseEngineFailure(new EngineFailureEventArgs(null, EngineFailureStatus.ShutdownSuccess));
                }
                catch (Exception ex)
                {
                    Log.Fatal<CordbEngine>(ex, "A fatal error occurred while processing the critical failure thread: {message}", ex.Message);

                    RaiseEngineFailure(new EngineFailureEventArgs(ex, EngineFailureStatus.ShutdownFailure));

                    //No need to throw
                }
            });
        }

        private void AnyManagedEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            //If we're already disposed, no point trying to handle this incoming event.
            //Session may be null now too
            if (disposed)
                return;

            var session = Session;

            //If another callback didn't add a more specific history item, add one now
            if (session.CallbackContext.NeedHistory)
                session.EventHistory.Add(CordbManagedEventHistoryItem.New(e));

            //Even when we call Stop() to break into the process, if we're in the middle of processing an event, we'll end up calling
            //Continue() again here. Process could be null if we've crashed and have disposed it
            if ((!session.StartupFailed && Process?.HasQueuedCallbacks == true) || (session.StartupFailed && e.Controller is CorDebugProcess p && p.HasQueuedCallbacks(null)))
            {
                /* Should we call continue here? Yes absolutely
                 *
                 * An important concept in ICorDebug is that of "synchronization". When a process is fully stopped, it is considered to be synchronized.
                 * When it may be freely running, it is unsynchronized. Many ICorDebug APIs will assert that the ICorDebugProcess is in a synchronized
                 * (i.e. stopped) state when you try and use them.
                 *
                 * When CordbRCEventThread::ThreadProc() sees that we have an event, it calls CordbRCEventThread::FlushQueuedEvents. For each
                 * event in the queue, we call CordbProcess::DispatchRCEvent(). DispatchRCEvent then does two key things
                 * 1. marks the process as synchronized and increments the stop count
                 * 2. For extra safety, declares a StopContinueHolder that executes an extra Stop and Continue before and after the managed callback event is dispatched
                 *
                 * Thus, on each managed callback, the stop count will effectively be two. If your managed callback does not call Continue, stop #1 above won't
                 * be cleared, and the process will remain in a synchronized state after your callback ends. Once DispatchRCEvent ends, FlushQueuedEvents checks whether
                 * the process is now unsynchronized (i.e. running). If so, it dispatches the next event in the queue. If the process is still stopped, it dispatches the
                 * next event. It's also worth noting that Continue only sets a process so synchronized when it is the one that decremented the stop count back to 0.
                 * Additionally, if Continue sees that there are managed callbacks queued, it will apparently signal that these queued events should be processed immediately.
                 * As noted above however, due to the implementation detail of StopContinueHolder, technically speaking it would appear that, contrary to popular belief,
                 * you WON'T suddenly start getting additional events immediately upon calling Continue(). Never-the-less, the contract of ICorDebug is "don't do anything after continue",
                 * so we won't. */

                DoContinue(e.Controller, false);
                return;
            }
            else
            {
                //At this point, if we're attaching, there are no more queued callbacks remaining, and we can now say that we're ready to go
                if (session.IsAttaching)
                {
                    //If we're crashing, the process may have already been cleared during the shutdown process
                    if (!session.StartupFailed && !session.IsCrashing)
                    {
                        //Now that we're no longer in the process of attaching, let's finally identify all of our special threads
                        Process.Threads.IdentifySpecialThreads(null);
                    }

                    session.IsAttaching = false;
                }
            }

            //The Any event is processed last. If any of our other event handlers objected
            //to continuing, the will have set Continue to false
            if (e.Continue)
            {
                DoContinue(e.Controller, false);
            }
            else
            {
                //We may not even have a Process if we crashed during CordbLauncher, and the user's EngineStatusChanged event handler may depend on that
                if (!session.IsCrashing)
                    OnStopping(false);
            }
        }

        private void AnyUnmanagedEvent(object sender, DebugEventCorDebugUnmanagedCallbackEventArgs e)
        {
            var session = Session;

            //If another callback didn't add a more specific history item, add one now
            if (session.CallbackContext.NeedHistory)
                session.EventHistory.Add(CordbNativeEventHistoryItem.New(e));

            //If we're trying to do evil things like step through the CLR, we'll have modified the OOB status.
            //But if we're in the middle of crashing, we may not have set the callback context's UnmanagedOutOfBand
            //property properly, and since we won't be dispatching normal event callbacks anyway, just go with the "normal" OOB status
            var outOfBand = session.IsCrashing ? e.OutOfBand : session.CallbackContext.UnmanagedOutOfBand;

            if (session.IsCrashing || session.CallbackContext.UnmanagedContinue)
            {
                DoContinue(Process?.CorDebugProcess ?? session.CrashingProcess, outOfBand, isUnmanaged: true);
            }
            else
            {
                //We want to stop. If this is an out of band event however, we can't stop, else the CLR will lock up. So instead, we'll increment our desired stop count.
                //As soon as we stop processing unmanaged events, we'll actually stop
                if (outOfBand)
                {
                    session.CallbackStopCount++;
                    DoContinue(Process.CorDebugProcess, outOfBand: true, isUnmanaged: true);
                }
                else
                {
                    //Not out of band. We successfully stopped!
                    OnStopping(true);
                }
            }
        }

        private void OnStopping(bool unmanaged)
        {
            Debug.Assert(!disposed, "How are we disposed already if managed events are still being pumped? We need to cleanup mscordbi prior to disposing!");

            lock (Session.UserPauseCountLock)
            {
                Log.Debug<CordbEngine>("Stopping");

                //We're not continuing. Update debugger state

                if (!Session.IsCrashing && Session.IsCLRLoaded)
                {
                    /* If we haven't had a single managed event yet, we can't refresh the DAC as attempting to
                     * create an SOSDacInterface will involve looking up the location of the clr loaded in the
                     * current process. By the time we've had a managed event, we know it's safe to start talking
                     * to the DAC. */
                    Process.DAC.Threads.Refresh();

                    /* Regardless of how we try and stop the engine, we need to assert that we've added a reason that we're stopping.
                     * When we ExitProcess and Continue, we get a CORDBG_E_PROCESS_TERMINATED response, which is OK. But when we get a DebuggerError
                     * whose ErrorHR is CORDBG_E_PROCESS_TERMINATED, continuing will give an CORDBG_E_UNRECOVERABLE_ERROR, which we don't want */
                    Session.CallbackContext.EnsureHasStopReason(unmanaged);
                }

                Log.Debug<CordbEngine>("Stop Reason: {reason}", Session.EventHistory.LastStopReason);

                Session.UserPauseCount++;

                NotifyEngineStatus();
            }
        }

        private void NotifyEngineStatus()
        {
            var currentStatus = Session.Status;

            var oldlastStatus = Session.LastStatus;

            if (oldlastStatus != currentStatus)
            {
                Log.Debug<CordbEngine>($"{oldlastStatus} -> {currentStatus}");

                Session.LastStatus = currentStatus;

                RaiseEngineStatusChanged(new EngineStatusChangedEventArgs(oldlastStatus, currentStatus));
            }
        }

        private void AddManagedPause(CorDebugManagedCallbackEventArgs e, CordbManagedEventPauseReason reason)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            if (reason == null)
                throw new ArgumentNullException(nameof(reason));

            e.Continue = false;

            Session.EventHistory.Add(reason);
        }

        private void AddUnmanagedPause(CordbNativeEventPauseReason reason)
        {
            Session.CallbackContext.UnmanagedContinue = false;

            Session.EventHistory.Add(reason);
        }
    }
}
