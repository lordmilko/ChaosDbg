using System;
using System.Diagnostics;
using System.Linq;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    class NetCoreProcess : NetInitCommon
    {
        #region Create

        public static void Create(
            CreateProcessOptions createProcessOptions,
            InitCallback initCallback)
        {
            WithDbgShim(dbgShim =>
            {
                CreateProcess(createProcessOptions, out var pi);

                var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);
                ValidateTargetArchitecture(pi.dwProcessId, is32Bit);

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
                    CreateFromCLR(createProcessOptions, pi, dbgShim, is32Bit, initCallback);
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

        private static void CreateFromCLR(
            CreateProcessOptions createProcessOptions,
            PROCESS_INFORMATION pi,
            DbgShim dbgShim,
            bool is32Bit,
            InitCallback initCallback)
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
                var corDebug = dbgShim.CreateDebuggingInterfaceFromVersionEx(CorDebugInterfaceVersion.CorDebugVersion_4_0, versionStr);

                //Initialize ICorDebug, setup our managed callback and attach to the existing process. We attach while the CLR is blocked waiting for the "continue" event to be called
                SetupCorDebug(createProcessOptions.CommandLine, corDebug, pi.dwProcessId, is32Bit, initCallback);

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

        private static void CreateProcess(CreateProcessOptions createProcessOptions, out PROCESS_INFORMATION pi)
        {
            GetCreateProcessArgs(createProcessOptions, out var creationFlags, out var si);

            Kernel32.CreateProcessW(
                createProcessOptions.CommandLine,
                creationFlags,
                IntPtr.Zero,
                Environment.CurrentDirectory,
                ref si,
                out pi
            );
        }

        #endregion
        #region Attach

        public static void Attach(
            AttachProcessOptions attachProcessOptions,
            InitCallback initCallback)
        {
            var pid = attachProcessOptions.ProcessId;

            var process = Process.GetProcessById(pid);
            var is32Bit = Kernel32.IsWow64ProcessOrDefault(process.Handle);
            ValidateTargetArchitecture(pid, is32Bit);

            //It's either a .NET Core process, or a purely unmanaged process. Check if coreclr is loaded
            WithDbgShim(dbgShim =>
            {
                var clrs = dbgShim.EnumerateCLRs(pid);

                if (clrs.Items.Length == 0)
                    throw new DebuggerInitializationException($"Cannot attach to process {pid}: target is not a managed process", HRESULT.E_FAIL);

                var runtime = clrs.Items.Single();

                var versionStr = dbgShim.CreateVersionStringFromModule(pid, runtime.Path);

                var corDebug = dbgShim.CreateDebuggingInterfaceFromVersionEx(CorDebugInterfaceVersion.CorDebugVersion_4_0, versionStr);

                //Now do the rest of the normal setup that we normally do in the CreateProcess pathway
                SetupCorDebug(null, corDebug, pid, is32Bit, initCallback);
            });
        }

        #endregion

        private static void SetupCorDebug(
            string commandLine,
            CorDebug corDebug,
            int processId,
            bool is32Bit,
            InitCallback initCallback)
        {
            corDebug.Initialize();

            var cb = new CordbManagedCallback();

            corDebug.SetManagedHandler(cb);

            var process = corDebug.DebugActiveProcess(processId, false);

            var target = new CordbTargetInfo(commandLine, processId, process, is32Bit);

            initCallback(cb, corDebug, target);
        }

        private static void ValidateTargetArchitecture(int pid, bool is32Bit)
        {
            if (IntPtr.Size == 4)
            {
                if (!is32Bit)
                    throw new DebuggerInitializationException($"Failed to attach to process {pid}: target process is 64-bit however debugger process is 32-bit.", HRESULT.E_FAIL);
            }
            else
            {
                if (is32Bit)
                    throw new DebuggerInitializationException($"Failed to attach to process {pid}: target process is 32-bit however debugger process is 64-bit.", HRESULT.E_FAIL);
            }
        }
    }
}
