using System;
using System.Linq;
using System.Runtime.InteropServices;
using ChaosDbg.Metadata;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void ThreadProc(DbgLaunchInfo launchInfo)
        {
            /* Launch the process suspended, store all required state, and resume the process.
             * .NET Core requires we launch the process first and then extract an ICorDebug from it,
             * while .NET Framework creates an ICorDebug first and then launches the process directly
             * inside of it. All required initialization must be done by the time this method returns,
             * as our managed callbacks are going to immediately start running
             */
            CreateDebugTarget(launchInfo);
        }

        #region CreateDebugTarget

        private void CreateDebugTarget(DbgLaunchInfo launchInfo)
        {
            //Is the target executable a .NET Framework or .NET Core process?

            var kind = exeTypeDetector.Detect(launchInfo.ProcessName);

            switch (kind)
            {
                case ExeKind.Native: //The user specifically requested .NET debugging; we will assume it's a self extracting single file executable
                case ExeKind.NetCore:
                    CreateNetCoreDebugTarget(launchInfo);
                    break;

                case ExeKind.NetFramework:
                    CreateNetFrameworkDebugTarget(launchInfo);
                    break;

                default:
                    throw new UnknownEnumValueException(kind);
            }
        }

        #region NetCore

        private void CreateNetCoreDebugTarget(DbgLaunchInfo launchInfo)
        {
            var dbgShimPath = DbgShimResolver.Resolve();

            var hDbgShim = Kernel32.LoadLibrary(dbgShimPath);

            try
            {
                var dbgShim = new DbgShim(hDbgShim);

                CreateProcess(launchInfo, out var pi);

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
                    CreateNetCoreDebugTargetFromCLR(launchInfo, pi, dbgShim);
                }
                finally
                {
                    if (pi.hProcess != IntPtr.Zero)
                        Kernel32.CloseHandle(pi.hProcess);

                    if (pi.hThread != IntPtr.Zero)
                        Kernel32.CloseHandle(pi.hThread);
                }
            }
            finally
            {
                Kernel32.FreeLibrary(hDbgShim);
            }
        }

        private void CreateNetCoreDebugTargetFromCLR(DbgLaunchInfo launchInfo, PROCESS_INFORMATION pi, DbgShim dbgShim)
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
                SetupNetCoreCorDebug(launchInfo, corDebug, pi);

                //Now that CorDebug is all initialized, store it in the session
                Session.CorDebug = corDebug;

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

        private void SetupNetCoreCorDebug(DbgLaunchInfo launchInfo, CorDebug corDebug, PROCESS_INFORMATION pi)
        {
            var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

            corDebug.Initialize();

            Session.ManagedCallback = new CordbManagedCallback();
            RegisterCallbacks(Session.ManagedCallback);
            corDebug.SetManagedHandler(Session.ManagedCallback);

            var process = corDebug.DebugActiveProcess(pi.dwProcessId, false);

            Target = new CordbTargetInfo(launchInfo.ProcessName, pi.dwProcessId, process, is32Bit);
        }

        #endregion
        #region NetFramework

        private void CreateNetFrameworkDebugTarget(DbgLaunchInfo launchInfo)
        {
            //Initialize is automatically called
            var corDebug = new CorDebug();

            //Setup callbacks
            Session.ManagedCallback = new CordbManagedCallback();
            RegisterCallbacks(Session.ManagedCallback);
            corDebug.SetManagedHandler(Session.ManagedCallback);

            //Create the process

            GetCreateProcessArgs(launchInfo, out var creationFlags, out var si);

            var pi = new PROCESS_INFORMATION();

            var process = corDebug.CreateProcess(
                lpApplicationName: null,
                lpCommandLine: launchInfo.ProcessName,
                lpProcessAttributes: default,
                lpThreadAttributes: default,
                bInheritHandles: true,
                dwCreationFlags: creationFlags,
                IntPtr.Zero,
                lpCurrentDirectory: null,
                si,
                ref pi,
                CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS
            );

            Session.CorDebug = corDebug;

            try
            {
                var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

                Target = new CordbTargetInfo(launchInfo.ProcessName, pi.dwProcessId, process, is32Bit);

                //We are now going to resume the process. Any required setup must now be complete or we will encounter a race
                Kernel32.ResumeThread(pi.hThread);
            }
            finally
            {
                if (pi.hProcess != IntPtr.Zero)
                    Kernel32.CloseHandle(pi.hProcess);

                if (pi.hThread != IntPtr.Zero)
                    Kernel32.CloseHandle(pi.hThread);
            }
        }

        #endregion

        private void CreateProcess(DbgLaunchInfo launchInfo, out PROCESS_INFORMATION pi)
        {
            GetCreateProcessArgs(launchInfo, out var creationFlags, out var si);

            Kernel32.CreateProcessW(
                launchInfo.ProcessName,
                creationFlags,
                IntPtr.Zero,
                Environment.CurrentDirectory,
                ref si,
                out pi
            );
        }

        private void GetCreateProcessArgs(
            DbgLaunchInfo launchInfo,
            out CreateProcessFlags creationFlags,
            out STARTUPINFOW si)
        {
            si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>()
            };

            if (launchInfo.StartMinimized)
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

        #endregion
    }
}
