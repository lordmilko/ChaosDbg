using System;
using System.Linq;
using ChaosLib;
using ClrDebug;
using Win32Process = System.Diagnostics.Process;

namespace ChaosDbg.Cordb
{
    class CordbCoreLauncher : CordbLauncher
    {
        #region Create

        protected override void CreateInternal()
        {
            WithDbgShim(dbgShim =>
            {
                CreateProcess(out var pi);

                processId = pi.dwProcessId;
                is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);
                ValidateTargetArchitecturePreAttach(false);

                try
                {
                    //RegisterForRuntimeStartup() has a race condition, because it calls GetStartupNotificationEvent
                    //internally, AFTER you've resumed the thread here. GetStartupNotificationEvent() must be called BEFORE
                    //ResumeThread is called, which can only be achieved by doing things manually
                    var startupEvent = dbgShim.GetStartupNotificationEvent(pi.dwProcessId);

                    //The event WaitForSingleObject is waiting on won't occur unless the process is resumed
                    dbgShim.ResumeProcess(pi.hThread);

                    //As stated above, if you started the process suspended, you need to resume the process otherwise the CLR will never be loaded.
                    var waitResult = Kernel32.WaitForSingleObject(startupEvent, 5000);

                    if (waitResult != WAIT.OBJECT_0)
                        throw new InvalidOperationException($"Failed to get startup event. Is the target process a .NET Core application? Wait Result: {waitResult}");

                    //The CLR is now loaded and the process is now hung waiting for us to signal g_hContinueStartupEvent
                    CreateFromCLR(pi, dbgShim);
                }
                finally
                {
                    if (pi.hProcess != IntPtr.Zero)
                        Kernel32.CloseHandle(pi.hProcess);

                    if (pi.hThread != IntPtr.Zero)
                        Kernel32.CloseHandle(pi.hThread);
                }
            });
        }

        private void CreateFromCLR(
            PROCESS_INFORMATION pi,
            DbgShim dbgShim)
        {
            var enumResult = dbgShim.EnumerateCLRs(pi.dwProcessId);

            try
            {
                var runtime = enumResult.Items.Single();

                //Version String is a comma delimited value containing dbiVersion, pidDebuggee, hmodTargetCLR
                var versionStr = dbgShim.CreateVersionStringFromModule(pi.dwProcessId, runtime.Path);

                /* Cordb::CheckCompatibility seems to be the only place where our debugger version is actually used,
                 * and it says that if the version is 4, its major version 4. Version 4.5 is treated as an "unrecognized future version"
                 * and is assigned major version 5, which is wrong. Cordb::CheckCompatibility then calls CordbProcess::IsCompatibleWith
                 * which doesn't actually seem to do anything either, despite what all the docs in it would imply. */
                corDebug = dbgShim.CreateDebuggingInterfaceFromVersionEx(CorDebugInterfaceVersion.CorDebugVersion_4_0, versionStr);

                //Initialize ICorDebug, setup our managed callback and attach to the existing process. We attach while the CLR is blocked waiting for the "continue" event to be called
                corDebug.Initialize();
                AttachCommon(true);

                /* There exists a structure CLR_ENGINE_METRICS within in coreclr.dll which is exported at ordinal 2. This structure indicates the RVA of the actual continue event that should be signalled
                 * to indicate the CLR can continue starting. But how does the CLR know to wait on this event at all? In debugger.cpp!NotifyDebuggerOfStartup() it calls
                 * OpenStartupNotificationEvent(). If that returns the event that was created by GetStartupNotificationEvent() then that event is set and closed,
                 * and then g_hContinueStartupEvent is waited on infinitely. g_hContinueStartupEvent is one of the components that make up the CLR_ENGINE_METRICS g_CLREngineMetrics,
                 * hence it all comes full circle. */
                Kernel32.SetEvent(runtime.Handle);
            }
            finally
            {
                //CloseCLREnumeration does not call WakeRuntimes(), hence we MUST call SetEvent above.
                //WakeRuntimes is called in InvokeStartupCallback() and UnregisterForRuntimeStartup() -> Unregister()
                dbgShim.CloseCLREnumeration(enumResult);
            }
        }

        private unsafe void CreateProcess(out PROCESS_INFORMATION pi)
        {
            GetCreateProcessArgs(out var creationFlags, out var si);

            //Disable the use of R2R images so that we can step through them properly
            options.EnvironmentVariables[WellKnownEnvironmentVariable.COMPlus_ReadyToRun] = "0";

            var environment = GetEnvironmentBytes();

            fixed (byte* pEnvironment = environment)
            {
                Kernel32.CreateProcessW(
                    options.CommandLine,
                    creationFlags,
                    (IntPtr) pEnvironment,
                    Environment.CurrentDirectory,
                    ref si,
                    out pi
                );
            }

            Log.Debug<CordbProcess>("Launched process {debuggeePid}", pi.dwProcessId);
        }

        #endregion

        protected override void AttachInternal()
        {
            var process = Win32Process.GetProcessById(options.ProcessId);
            is32Bit = Kernel32.IsWow64ProcessOrDefault(process.Handle);
            ValidateTargetArchitecturePreAttach(didCreateProcess: false);

            //It's either a .NET Core process, or a purely unmanaged process. Check if coreclr is loaded
            WithDbgShim(dbgShim =>
            {
                var clrs = dbgShim.EnumerateCLRs(processId.Value);

                if (clrs.Items.Length == 0)
                    throw new DebuggerInitializationException($"Cannot attach to process {processId.Value}: target is not a managed process", HRESULT.E_FAIL);

                var runtime = clrs.Items.Single();

                var versionStr = dbgShim.CreateVersionStringFromModule(processId.Value, runtime.Path);

                corDebug = dbgShim.CreateDebuggingInterfaceFromVersionEx(CorDebugInterfaceVersion.CorDebugVersion_4_0, versionStr);
                LogNativeCorDebugInfo(corDebug);

                //Now do the rest of the normal setup that we normally do in the CreateProcess pathway
                corDebug.Initialize();
                AttachCommon(false);
            });
        }

        private void WithDbgShim(Action<DbgShim> action)
        {
            //Locate dbgshim, which should be in our output directory
            var dbgShimPath = DbgShimResolver.Resolve();

            //Load dbgshim, do something with it, then unload

            var hDbgShim = Kernel32.LoadLibrary(dbgShimPath);

            try
            {
                var dbgShim = new DbgShim(hDbgShim);

                action(dbgShim);
            }
            finally
            {
                Kernel32.FreeLibrary(hDbgShim);
            }
        }

        private void ValidateTargetArchitecturePreAttach(bool didCreateProcess)
        {
            void KillProcess()
            {
                if (!didCreateProcess)
                    return;

                //If we can't attach to the process we created, terminate the process

                try
                {
                    Win32Process.GetProcessById(processId.Value).Kill();
                }
                catch
                {
                    //The process is already terminated. Great!
                }
            }

            if (IntPtr.Size == 4)
            {
                if (!is32Bit.Value)
                {
                    KillProcess();
                    throw new DebuggerInitializationException($"Failed to attach to process {processId.Value}: target process is 64-bit however debugger process is 32-bit.", HRESULT.E_FAIL);
                }
            }
            else
            {
                if (is32Bit.Value)
                {
                    KillProcess();
                    throw new DebuggerInitializationException($"Failed to attach to process {processId.Value}: target process is 32-bit however debugger process is 64-bit.", HRESULT.E_FAIL);
                }
            }
        }
    }
}
