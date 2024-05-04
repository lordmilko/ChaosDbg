using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ChaosDbg.Metadata;
using ChaosDbg.TypedData;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.TypedData;
using ClrDebug;
using Win32Process = System.Diagnostics.Process;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    abstract class CordbLauncher
    {
        private CordbEngine engine;

        protected CorDebug corDebug;
        protected CorDebugProcess corDebugProcess;
        protected CordbManagedCallback cb;
        protected CordbUnmanagedCallback ucb;

        protected LaunchTargetOptions options;

        //Nullable so that we can assert that we've set them in all code paths that require them
        protected int? processId;
        protected bool? is32Bit;

        protected Thread startupThread;
        private CancellationTokenSource launchCTS;

        public static void Create(LaunchTargetOptions options, CordbEngine engine, CancellationToken token)
        {
            /* Launch the process suspended, store all required state, and resume the process.
             * .NET Core requires we launch the process first and then extract an ICorDebug from it,
             * while .NET Framework creates an ICorDebug first and then launches the process directly
             * inside of it. All required initialization must be done by the time this method returns,
             * as our managed callbacks are going to immediately start running */

            CordbLauncher launcher;

            switch (options.FrameworkKind.Value)
            {
                case FrameworkKind.Native: //The user specifically requested .NET debugging; we will assume it's a self extracting single file executable
                case FrameworkKind.NetCore:
                    launcher = new CordbCoreLauncher();
                    break;

                case FrameworkKind.NetFramework:
                    launcher = new CordbFrameworkLauncher();
                    break;

                default:
                    throw new UnknownEnumValueException(options.FrameworkKind.Value);
            }

            launcher.options = options;
            launcher.engine = engine;
            launcher.startupThread = Thread.CurrentThread;
            launcher.launchCTS = CancellationTokenSource.CreateLinkedTokenSource(token);

            launcher.CreateInternal();
        }

        public static void Attach(LaunchTargetOptions options, CordbEngine engine, CancellationToken token)
        {
            var process = Win32Process.GetProcessById(options.ProcessId);

            process.Exited += (s, e) => Log.Debug<Win32Process>("Process {pid} exited with code {code}", process.Id, process.ExitCode);
            process.EnableRaisingEvents = true;

            CordbLauncher launcher;

            if (process.Modules.Cast<ProcessModule>().Any(m => m.ModuleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase)))
                launcher = new CordbFrameworkLauncher();
            else
                launcher = new CordbCoreLauncher();

            launcher.options = options;
            launcher.engine = engine;
            launcher.processId = options.ProcessId;
            launcher.startupThread = Thread.CurrentThread;
            launcher.launchCTS = CancellationTokenSource.CreateLinkedTokenSource(token);

            launcher.AttachInternal();
        }

        protected abstract void CreateInternal();

        protected abstract void AttachInternal();

        protected void AttachCommon(bool hackClrInstanceId)
        {
            cb = new CordbManagedCallback();
            InstallManagedStartupHook();
            corDebug.SetManagedHandler(cb);

            ManualResetEventSlim wait = null;

            if (options.UseInterop)
            {
                ucb = new CordbUnmanagedCallback();

                //Don't let unmanaged callbacks run free until we've signalled we're ready!
                InstallInteropStartupHook(out wait);

                corDebug.SetUnmanagedHandler(ucb);
            }

            var threads = SuspendAllThreads();

            try
            {
                //Register the callbacks before attaching, because if we're doing interop debugging, even though the process is suspended the OS may send us
                //a CREATE_PROCESS_DEBUG_EVENT immediately, so we need to make sure we've already registered our normal event handler for it
                RegisterCallbacks();

                //Do the attach
                var hr = corDebug.TryDebugActiveProcess(processId.Value, options.UseInterop, out corDebugProcess);
                LogNativeCorDebugProcessInfo(corDebugProcess);

                ValidatePostCreateOrAttach(hr, $"attach to process {processId}");

                //.NET Core attach will have already retrieved whether the target is 32-bit or not
                if (is32Bit == null)
                    is32Bit = Kernel32.IsWow64ProcessOrDefault(corDebugProcess.Handle);

                //If we crash during startup, we'll need to resume all the threads so that we can call CordbProcess::Stop() and then throw
                StoreSessionInfo(threads);

                var sw = new Stopwatch();
                sw.Start();

#if DEBUG
                if (options.UseInterop && hackClrInstanceId)
                    TryHackClrInstanceId();
#endif

                sw.Stop();

                ResumeAllThreads(threads);
            }
            finally
            {
                foreach (var handle in threads)
                    handle.Dispose();
            }

            wait?.Set();
        }

        protected void GetCreateProcessArgs(
            out CreateProcessFlags creationFlags,
            out STARTUPINFOW si)
        {
            si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>()
            };

            if (options.StartMinimized)
            {
                //Specifies that CreateProcess should look at the settings specified in wShowWindow
                si.dwFlags = STARTF.STARTF_USESHOWWINDOW;

                //We use ShowMinNoActive here instead of ShowMinimized, as ShowMinimized has the effect of causing our debugger
                //window to flash, losing and then regaining focus. If we never active the newly created process, we never lose
                //focus to begin with
                si.wShowWindow = ShowWindow.ShowMinNoActive;
            }

            creationFlags =
                CreateProcessFlags.CREATE_NEW_CONSOLE | //In the event ChaosDbg is invoked via some sort of command line tool, we want our debuggee to be created in a new window
                CreateProcessFlags.CREATE_SUSPENDED;    //Don't let the process start running; after we create it we want our debugger to attach to it
        }

        private SafeThreadHandle[] SuspendAllThreads()
        {
            Log.Debug<CordbLauncher>("Suspending all threads");

            /* Since the debuggee is already running, we could receive a debugger event immediately after attaching, but before we've stored our
             * session state. Threads could constantly keep popping up each time we ask for threads, so instead what
             * we do is we keep polling for threads, suspending them as we go, until there we no new threads we haven't
             * seen before. At that point, we know that we have all threads */

            var knownThreads = new HashSet<int>();
            var threadHandles = new List<SafeThreadHandle>();

            bool hadChanges;

            do
            {
                hadChanges = false;

                var currentThreads = Process.GetProcessById(processId.Value).Threads;

                foreach (ProcessThread thread in currentThreads)
                {
                    if (knownThreads.Add(thread.Id))
                    {
                        hadChanges = true;

                        if (Kernel32.TryOpenThread(ThreadAccess.SUSPEND_RESUME, false, thread.Id, out var threadHandle) == S_OK)
                        {
                            //If we can't suspend the thread, we'll assume the thread must have died or something
                            Kernel32.TrySuspendThread(threadHandle);

                            threadHandles.Add(threadHandle);
                        }
                    }
                }
            } while (hadChanges);

            return threadHandles.ToArray();
        }

        protected void InstallManagedStartupHook()
        {
            EventHandler<CorDebugManagedCallbackEventArgs> onPreEvent = null;

            onPreEvent = (s, e) =>
            {
                //If an unrecoverable error occurs prior to this event handler being called, we may get a DebuggerError event
                //on the Win32 Event Thread
                if (Thread.CurrentThread.Name == null)
                    Thread.CurrentThread.Name = $"Managed Callback Thread {engine.Session.EngineId}"; //Called the "Runtime Controller" thread in ICorDebug

                Log.CopyContextFrom(engine.Session.EngineThread);

                cb.OnPreEvent -= onPreEvent;
            };

            //We are the first event handler to register ourselves on this event, so we will be triggered before the normal pre-event handler hooked up in RegisterCallbacks() later on
            cb.OnPreEvent += onPreEvent;
        }

        protected void InstallInteropStartupHook(out ManualResetEventSlim wait)
        {
            /* Regardless of whether we're attaching or creating the process from scratch, the Win32 Event Thread will
             * begin waiting on WaitForDebugEvent immediately, which may cause us to receive our CREATE_PROCESS_DEBUG_EVENT before
             * we've stored our session state. Thus, we need to block until we signal that we're ready to receive the event */

            EventHandler<DebugEventCorDebugUnmanagedCallbackEventArgs> onPreEvent = null;

            wait = new ManualResetEventSlim(false);

            //We can't use a ref variable in a lambda expression
            var localWait = wait;

            onPreEvent = (s, e) =>
            {
                InitializeWin32EventThreadContext();

                //We're either waiting for AttachCommon to signal we're good to go, or for the engine to indicate it's actually
                //crashed during startup, and we should forget about the whole thing!

                Log.Debug<CordbLauncher>("Waiting for engine thread to signal that it is ready to receive unmanaged events");
                WaitHandle.WaitAny(new[] { launchCTS.Token.WaitHandle, localWait.WaitHandle });

                ucb.OnPreEvent -= onPreEvent;

                //No need to throw. We want to ensure our normal pre-event handler runs
                if (engine.Session.StartupFailed)
                    return;

                launchCTS.Token.ThrowIfCancellationRequested();
            };

            //We are the first event handler to register ourselves on this event, so we will be triggered before the normal pre-event handler hooked up in RegisterCallbacks() later on
            ucb.OnPreEvent += onPreEvent;
        }

        private static void ResumeAllThreads(SafeThreadHandle[] threads)
        {
            Log.Debug<CordbLauncher>("Resuming all threads");

            foreach (var thread in threads)
                Kernel32.TryResumeThread(thread);
        }

        protected void ValidatePostCreateOrAttach(HRESULT hr, string action)
        {
            switch (hr)
            {
                case HRESULT.ERROR_NOT_SUPPORTED: //A x64 target was launched from a x86 debugger
                case HRESULT.CORDBG_E_INCOMPATIBLE_PLATFORMS: //A x86 target was launched from a x64 debugger
                    throw new DebuggerInitializationException($"Failed to {action}: process most likely does not match architecture of debugger ({(IntPtr.Size == 4 ? "x86" : "x64")}).", hr);

                default:
                    hr.ThrowOnNotOK();
                    break;
            }
        }

        protected void RegisterCallbacks()
        {
            Log.Debug<CordbLauncher>("Registering callbacks");

            engine.RegisterCallbacks(cb);
            engine.RegisterUnmanagedCallbacks(ucb);
        }

        protected void StoreSessionInfo(SafeThreadHandle[] threads)
        {
            try
            {
                engine.Session.CorDebug = corDebug;
                engine.Session.ManagedCallback = cb;
                engine.Session.UnmanagedCallback = ucb;
                engine.Session.Process = new CordbProcess(
                    corDebugProcess,
                    engine.Session,
                    is32Bit.Value,
                    options.IsAttach ? null : options.CommandLine
                );
                engine.Session.IsInterop = options.UseInterop;

                Log.Debug<CordbLauncher>("Successfully stored session info");
            }
            catch (Exception ex)
            {
                //CordbProcess::Terminate merely "asks nicely" for the target process to terminate (see the comments in the Shutdown region of CordbEngine). The process is not truly terminated until the EXIT_PROCESS_DEBUG_EVENT is received.
                //This is a bit of an issue, because in PreEventCommon we assert that the the session process should always be non-null.
                Log.Error<CordbLauncher>(ex, "Failed to store session info: {message}", ex.Message);

                //We failed to initialize properly. Stop and terminate the process.

                engine.Session.StartupFailed = true;
                engine.Session.SetCrashingProcess(corDebugProcess);

                //Advise our callbacks to skip all events but the unmanaged CreateProcess event (so that we can initialize our logger) and the managed ExitProcess event (which we're going to be waiting on)
                var attachReadyToShutdown = cb.SetHadStartupFailure(options.IsAttach);
                ucb?.SetHadStartupFailure();

                HRESULT hr;

                /* In order to terminate the process, it first has to be synchronized. This must be done by calling CordbProcess::Stop(). StopInternal() will automatically
                 * check whether the process has executed any managed code yet (m_initialized) and if so will simply set the process to synchronized and return early. Otherwise,
                 * CordbProcess::StartSyncFromWin32Stop() will be called. This method will do two things. First, it will call CordbWin32EventThread::SendUnmanagedContinue() which will
                 * need to be handled on the Win32 event thread. Secondly, it will attempt to send an IPC event to the target process to tell it that we want to stop.
                 *
                 * When we're attaching, this causes two problems for us:
                 * 1. Even though we suspended all threads in the target process, the OS may have already begun dispatching fake attach events to us, and we're now blocked in InstallInteropAttachStartupHook waiting for
                 *    us to signal that we're ready to continue
                 *
                 * 2. We suspended all threads in the target process!
                 *
                 * Thus, we must now ensure that we unblock the Win32 event thread and resume the target process so that the left side can handle the IPC event that Stop() sends to it */
                launchCTS.Cancel();

                //No threads to resume when creating under .NET Framework
                if (threads != null)
                    ResumeAllThreads(threads);

                if (options.IsAttach)
                    attachReadyToShutdown.Wait();

                Log.Debug<CordbLauncher>("Stopping process...");
                hr = corDebugProcess.TryStop(0);

                Log.Debug<CordbLauncher>("ICorDebugProcess.Stop() returned with {hr}", hr);

                //As soon as we call terminate it's going to dispatch to the Win32 event thread an event requesting termination; thus, we want to ensure
                //we have our bookkeeping done first
                engine.Session.IsTerminating = true;

                hr = corDebugProcess.TryTerminate(0);

                if (hr == HRESULT.S_OK)
                    Log.Debug<CordbLauncher>("ICorDebugProcess.Terminate() returned with {hr}", hr);
                else
                    Log.Warning<CordbLauncher>("ICorDebugProcess.Terminate() returned with {hr}. Throwing.", hr);

                hr.ThrowOnNotOK();

                Log.Debug<CordbLauncher>("Waiting for exit process event...");
                engine.Session.WaitExitProcess.Wait();
                Log.Debug<CordbLauncher>("Got exit process event");

                //Now terminate CorDebug
                engine.Terminate();

                throw;
            }
        }

        protected byte[] GetEnvironmentBytes()
        {
            StringBuilder stringBuilder = new StringBuilder();

            var sd = options.EnvironmentVariables;

            var keys = new string[sd.Count];
            var values = new string[sd.Count];

            sd.Keys.CopyTo(keys, 0);
            sd.Values.CopyTo(values, 0);

            for (int index = 0; index < sd.Count; ++index)
            {
                stringBuilder.Append(keys[index]);
                stringBuilder.Append('=');
                stringBuilder.Append(values[index]);
                stringBuilder.Append(char.MinValue);
            }

            stringBuilder.Append(char.MinValue);

            return Encoding.ASCII.GetBytes(stringBuilder.ToString());
        }

        protected void InitializeWin32EventThreadContext()
        {
            Thread.CurrentThread.Name = $"Win32 Callback Thread {engine.Session.EngineId}";

            //If our token has already been cancelled, we'll immediately try and log in the critical failure thread without an existing context. As such, initialize the context before we have a chance to throw
            Log.CopyContextFrom(startupThread);
        }

        [Conditional("DEBUG")]
        private unsafe void TryHackClrInstanceId()
        {
            /* When interop debugging .NET Core processes, a number of C++ exceptions will be thrown prior to receiving the loader breakpoint.
             * When dbgshim!CreateDebuggingInterfaceFromVersionEx calls mscordbi!CoreCLRCreateCordbObjectEx it passes in a hmodTargetCLR.
             * This is a problem, because each time we get an unmanaged process event, CordbProcess::GetUnmanagedThreadFromEvent will
             * try and see whether the debugger control block on the left side has been initialized yet. CordbProcess::GetEventBlock()
             * will call CordbProcess::TryInitializeDac() -> CordbProcess::EnsureClrInstanceIdSet() which will return success due to
             * the fact we know the hModule of the CLR (from the hmodTargetCLR), and so can set a m_clrInstanceId from it.
             *
             * All of the initial threads in the process get loaded prior to coreclr being loaded, which occurs just before the loader breakpoint.
             * Thus, by calling CoreCLRCreateCordbObjectEx ourselves we can pass in a null hmodTargetCLR and allow mscordbi to instead
             * detect the CLR is loaded itself, after the loader breakpoint.
             *
             * This almost works, except for one small issue: there appears to be a bug in CordbWin32EventThread::AttachProcess() wherein it will just plow ahead
             * and try and access the dac (pProcess->GetDAC()->MetadataUpdatesApplied()) without checking whether or not the DAC is already loaded yet. There doesn't
             * seem to be any way of forcing the DAC to load. Even if we could get CordbProcess::CreateDacDbiInterface() / CordbProcess::InitializeDac() to be called,
             * CreateDacDbiInterface relies on the m_clrInstanceId anyway. m_clrInstanceId is only used inside this method, and other methods that only apply once
             * managed events have loaded. Thus, we attempt to do something pure evil: if we're interop debugging, we can quickly find the PDB of the coreclr in the target process,
             * and can find a symbol that gives us the address of Cordb::m_targetCLR and CordbProcess::m_clrInstanceId, then we'll clear all these out after DebugActiveProcess returns.
             *
             * Note that when ChaosDbg is run outside of Visual Studio, it's faster to let the exceptions occur than it is to try and prevent them via this technique (due to the cost involved
             * in loading mscordbi symbols). This is likely due to the fact that Visual Studio's exception handling is probably slowing things down a bit. */

            //This improves startup performance a bit when developing ChaosDbg, but otherwise slows it down a tiny amount when executing outside of a debugger
            if (!System.Diagnostics.Debugger.IsAttached)
                return;

            var remoteCorDebug = NativeReflector.GetTypedData(corDebug);
            var remoteCorDebugProcess = NativeReflector.GetTypedData(corDebugProcess);

            var m_targetCLR = remoteCorDebug["m_targetCLR"];
            var m_clrInstanceId = remoteCorDebugProcess["m_clrInstanceId"];

            var originalTargetCLR = (IntPtr) ((IDbgRemoteValue) m_targetCLR.Value).Address;
            m_targetCLR.Value = IntPtr.Zero;
            m_clrInstanceId.Value = (ulong) 0;

            EventHandler<DebugEventCorDebugUnmanagedCallbackEventArgs> onPreEvent = null;

            onPreEvent = (s, e) =>
            {
                //When we hit the loader BP, all the initial threads will have started. Restore things back the way they were

                if (e.DebugEvent.dwDebugEventCode == DebugEventType.EXCEPTION_DEBUG_EVENT && e.DebugEvent.u.Exception.ExceptionRecord.ExceptionCode == NTSTATUS.STATUS_BREAKPOINT)
                {
                    ucb.OnPreEvent -= onPreEvent;

                    m_targetCLR.Value = originalTargetCLR;
                }
            };

            ucb.OnPreEvent += onPreEvent;
        }

        [Conditional("DEBUG")]
        internal static void LogNativeCorDebugInfo(CorDebug corDebug)
        {
            try
            {
                var native = (DiaRemoteObject) NativeReflector.GetTypedData(corDebug);
                var rcEventThreadId = (uint) native["m_rcEventThread"]["m_threadId"].Value;
                Log.Debug<CordbLauncher>("Cordb created RC Event Thread {rcEventThreadId}", rcEventThreadId);
            }
            catch (Exception ex)
            {
                //Don't allow this to throw and crash the whole program
                Debug.Assert(false, ex.Message);
            }
        }

        [Conditional("DEBUG")]
        internal static void LogNativeCorDebugProcessInfo(CorDebugProcess corDebugProcess)
        {
            try
            {
                var native = (DiaRemoteObject) NativeReflector.GetTypedData(corDebugProcess);

                var pShim = native["m_pShim"];
                var ptr = pShim["m_ptr"];
                var win32ET = ptr["m_pWin32EventThread"];
                var win32EventThreadId = (uint) win32ET["m_threadId"].Value;
                Log.Debug<CordbLauncher>("ShimProcess created Win32 Event Thread {win32EventThreadId}", win32EventThreadId);
            }
            catch (Exception ex)
            {
                //Don't allow this to throw and crash the whole program
                Debug.Assert(false, ex.Message);
            }
        }
    }
}
