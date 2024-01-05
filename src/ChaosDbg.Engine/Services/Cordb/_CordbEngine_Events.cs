using System;
using System.Diagnostics;
using System.Threading;
using ClrDebug;
using static ChaosDbg.EventExtensions;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void RegisterCallbacks(CordbManagedCallback cb)
        {
            cb.OnPreEvent += PreManagedEvent;

            cb.OnCreateProcess += CreateProcess;
            cb.OnExitProcess += ExitProcess;
            cb.OnLoadModule += LoadModule;
            cb.OnUnloadModule += UnloadModule;
            cb.OnCreateThread += CreateThread;
            cb.OnExitThread += ExitThread;
            cb.OnNameChange += NameChange;

            cb.OnAnyEvent += AnyManagedEvent;
        }

        private void RegisterUnmanagedCallbacks(CordbUnmanagedCallback ucb)
        {
            if (ucb == null)
                return;

            ucb.OnPreEvent += PreUnmanagedEvent;

            ucb.OnAnyEvent += AnyUnmanagedEvent;
            ucb.OnCreateProcess += UnmanagedCreateProcess;
            ucb.OnLoadDll += UnmanagedLoadModule;
            ucb.OnUnloadDll += UnmanagedUnloadModule;
            ucb.OnCreateThread += UnmanagedCreateThread;
            ucb.OnExitThread += UnmanagedExitThread;
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

        private void PreManagedEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            PreEventCommon();
        }

        private void PreUnmanagedEvent(object sender, DebugEventCorDebugUnmanagedCallbackEventArgs e)
        {
            //Always do this first so that the callback context is overwritten
            PreEventCommon();

            //Store some information about who the event pertains to
            Session.CallbackContext.UnmanagedEventProcessId = e.DebugEvent.dwProcessId;
            Session.CallbackContext.UnmanagedEventThreadId = e.DebugEvent.dwThreadId;
        }

        private void PreEventCommon()
        {
            Debug.Assert(Session.Process != null, "Didn't have a process!");

            Session.CurrentStopCount++;
            Session.CallbackContext.Clear();
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

            //Called the "Runtime Controller" thread in ICorDebug
            Thread.CurrentThread.Name = "Managed Callback Thread";

            /* When attaching to a process, assuming that the attach isn't happening so early in the process' lifetime that a
             * real CreateProcess event hasn't even been fired yet, ShimProcess::QueueFakeAttachEvents will generate a number of "fake"
             * debug events in order to bring us up to speed on the status of the AppDomain, Assembly, Module and Thread objects in
             * the target process. */

            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process.
            //This will fail when attempting to attach
            e.Process.TrySetDesiredNGENCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);
        }

        private void ExitProcess(object sender, ExitProcessCorDebugManagedCallbackEventArgs e)
        {
            //On Windows, a process has not truly exited until we receive the EXIT_PROCESS_DEBUG_EVENT. My observation has been that, when interop debugging,
            //we always receive EXIT_PROCESS_DEBUG_EVENT prior to receiving the managed ExitProcess event. While I don't know whether a process handle being signalled
            //because it exited necessarily proves that EXIT_PROCESS_DEBUG_EVENT has also been fired. at this stage I don't see any reason why this would even matter

            //Signal the wait as completed
            Session.WaitExitProcess.SetResult(null);
        }

        private void UnmanagedCreateProcess(object sender, CREATE_PROCESS_DEBUG_INFO e)
        {
            /* Events are dispatched to us from a Win32 Event Thread. CordbWin32EventThread::Start() creates
             * a new thread that executes CordbWin32EventThread::ThreadProc() -> Win32EventLoop(). Win32EventLoop()
             * loops around calling ::WaitForDebugEvent. When it gets an event it calls ShimProcess::HandleWin32DebugEvent()
             * which in turn dispatches to CordbProcess::HandleDebugEventForInteropDebugging() */
            Thread.CurrentThread.Name = "Win32 Callback Thread";

            /* When attaching, NtDebugActiveProcess will call DbgkpPostFakeProcessCreateMessages to send us a bunch of fake
             * CREATE_PROCESS_DEBUG_EVENT, CREATE_THREAD_DEBUG_EVENT and LOAD_DLL_DEBUG_EVENT messages, immediately bringing
             * us up to speed */

            /* Register the thread for the main thread. There won't be a separate CREATE_THREAD_DEBUG_EVENT event for this.
             * When interop debugging, due to the fact you have to let the process start running before you can actually attach
             * to it, there will already be like 5 other threads running by the time this CREATE_PROCESS_DEBUG_EVENT is dispatched.
             * Not to worry, CREATE_THREAD_DEBUG_EVENT notifications for those threads will soon be incoming. */
            Process.Threads.Add(Session.CallbackContext.UnmanagedEventThreadId, e.hThread);

            Session.EventHistory.Add(CordbNativeEventHistoryItem.CreateProcess());
            Session.EventHistory.Add(CordbNativeEventHistoryItem.CreateThread(Session.CallbackContext.UnmanagedEventThreadId));

            //Ensure the process gets registered as a module too. This will also add the module event history item
            UnmanagedLoadModule(sender, new LOAD_DLL_DEBUG_INFO
            {
                hFile = e.hFile,
                lpBaseOfDll = e.lpBaseOfImage,
                dwDebugInfoFileOffset = e.dwDebugInfoFileOffset,
                nDebugInfoSize = e.nDebugInfoSize,
                lpImageName = e.lpImageName,
                fUnicode = e.fUnicode
            });
        }

        #endregion
        #region Module

        private void LoadModule(object sender, LoadModuleCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient

            //JIT gets in the way of stepping. Where at all possible, try and disable JIT when debugging a process
            e.Module.TrySetJITCompilerFlags(CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION);

            var module = Process.Modules.Add(e.Module);

            HandleUIEvent(ModuleLoad, new EngineModuleLoadEventArgs(module));
        }

        private void UnloadModule(object sender, UnloadModuleCorDebugManagedCallbackEventArgs e)
        {
            //Don't log event history here; the default handler is sufficient
            var module = Process.Modules.Remove(e.Module.BaseAddress);

            if (module != null)
                HandleUIEvent(ModuleUnload, new EngineModuleUnloadEventArgs(module));
        }

        private void UnmanagedLoadModule(object sender, LOAD_DLL_DEBUG_INFO e)
        {
            var module = Process.Modules.Add(e);

            Session.EventHistory.Add(new CordbNativeModuleLoadEventHistoryItem(module));
            HandleUIEvent(ModuleLoad, new EngineModuleLoadEventArgs(module));
        }

        private void UnmanagedUnloadModule(object sender, UNLOAD_DLL_DEBUG_INFO e)
        {
            var module = Process.Modules.Remove(e);

            if (module != null)
            {
                Session.EventHistory.Add(new CordbNativeModuleUnloadEventHistoryItem(module));
                HandleUIEvent(ModuleUnload, new EngineModuleUnloadEventArgs(module));
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

                HandleUIEvent(ThreadCreate, new EngineThreadCreateEventArgs(thread));
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
                    HandleUIEvent(ThreadExit, new EngineThreadExitEventArgs(thread));
            }
        }

        private void NameChange(object sender, NameChangeCorDebugManagedCallbackEventArgs e)
        {
            //Either an AppDomain or Thread name changed

            if (e.Thread != null)
            {
                /* It was a thread name change. As per the documentation on Debugger::NameChangeEvent(),
                 * in order to an AppDomain's name change, just do AppDomain.Name. But in order to get
                 * a Thread's name change, you're recommended to do a func-eval of Thread::get_Name!
                 * This is what dnSpy does as well. When you call Thread.Name = "value", it dispatches
                 * to ThreadNative::InformThreadNameChange(). This method does 3 things in the following order:
                 *
                 * 1. Calls SetThreadDescriptor()
                 * 2. Notifies the profiler via ICorProfilerCallback2::ThreadNameChanged()
                 * 3. Calls Debugger::NameChangeEvent().
                 *
                 * But there's a catch: because Get/SetThreadDescriptor were only introduced in
                 * Windows 10 1607, they aren't part of .NET Framework's version of ThreadNative::InformThreadNameChange()!
                 * As such, your only true option is to use func-eval. */

                //todo: requires func-eval
            }
        }

        private void UnmanagedCreateThread(object sender, CREATE_THREAD_DEBUG_INFO e)
        {
            //Don't log event history here; the default handler is sufficient

            var thread = Process.Threads.Add(Session.CallbackContext.UnmanagedEventThreadId, e.hThread);

            HandleUIEvent(ThreadCreate, new EngineThreadCreateEventArgs(thread));
        }

        private void UnmanagedExitThread(object sender, EXIT_THREAD_DEBUG_INFO e)
        {
            //Don't log event history here; the default handler is sufficient

            var thread = Process.Threads.Remove(Session.CallbackContext.UnmanagedEventThreadId);

            if (thread != null)
                HandleUIEvent(ThreadExit, new EngineThreadExitEventArgs(thread));
        }

        #endregion

        private void AnyManagedEvent(object sender, CorDebugManagedCallbackEventArgs e)
        {
            //If we're already disposed, no point trying to handle this incoming event.
            //Session may be null now too
            if (disposed)
                return;

            //If another callback didn't add a more specific history item, add one now
            if (Session.CallbackContext.NeedHistory)
                Session.EventHistory.Add(CordbManagedEventHistoryItem.New(e));

            //Even when we call Stop() to break into the process, if we're in the middle of processing an event, we'll end up calling
            //Continue() again here.
            if (Process.HasQueuedCallbacks)
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
                if (Session.IsAttaching)
                {
                    Process.Threads.IdentifySpecialThreads(null);
                    Session.IsAttaching = false;
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
                OnStopping();
            }
        }

        private void AnyUnmanagedEvent(object sender, DebugEventCorDebugUnmanagedCallbackEventArgs e)
        {
            //If another callback didn't add a more specific history item, add one now
            if (Session.CallbackContext.NeedHistory)
                Session.EventHistory.Add(CordbNativeEventHistoryItem.New(e));

            DoContinue(Process.CorDebugProcess, e.OutOfBand, isUnmanaged: true);
        }

        private void OnStopping()
        {
            //We're not continuing. Update debugger state

            if (Session.EventHistory.ManagedEventCount > 0)
            {
                //If we haven't had a single managed event yet, we can't refresh the DAC as attempting to
                //create an SOSDacInterface will involve looking up the location of the clr loaded in the
                //current process. By the time we've had a managed event, we know it's safe to start talking
                //to the DAC.
                Process.DAC.Threads.Refresh();
            }
        }

        private void SetEngineStatus(EngineStatus newStatus)
        {
            var oldStatus = Session.Status;

            if (oldStatus != newStatus)
            {
                Session.Status = newStatus;

                HandleUIEvent(EngineStatusChanged, new EngineStatusChangedEventArgs(oldStatus, newStatus));
            }
        }
    }
}
