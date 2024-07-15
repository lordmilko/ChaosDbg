using System;
using System.Threading;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbFrameworkLauncher : CordbLauncher
    {
        protected override unsafe void CreateInternal()
        {
            //Initialize is automatically called
            corDebug = new CorDebug();
            LogNativeCorDebugInfo(corDebug);

            //Setup callbacks
            cb = new CordbManagedCallback();
            InstallManagedStartupHook();
            corDebug.SetManagedHandler(cb);

            ManualResetEventSlim wait = null;

            if (options.UseInterop)
            {
                ucb = new CordbUnmanagedCallback();

                //It seems that even during Create, the Win32 Event Thread may immediately call WaitForDebugEvent, so we need to block
                //until we're ready to receive it
                InstallInteropStartupHook(out wait);

                corDebug.SetUnmanagedHandler(ucb);
            }

            //Create the process

            GetCreateProcessArgs(out var creationFlags, out var si);

            if (options.UseInterop)
            {
                //Required for interop debugging. MSDN claims you need to do DEBUG_PROCESS | DEBUG_ONLY_THIS_PROCESS however
                //rsmain.cpp!Cordb::CreateProcessCommon says this is wrong
                creationFlags |= CreateProcessFlags.DEBUG_ONLY_THIS_PROCESS;
            }

            var pi = new PROCESS_INFORMATION();

            //Disable the use of NGEN images (zap is the original codename for NGEN) so that we can step
            //through them properly
            options.EnvironmentVariables[WellKnownEnvironmentVariable.COMPlus_ZapDisable] = "1";
            var environment = GetEnvironmentBytes();

            HRESULT hr;

            fixed (byte* pEnvironment = environment)
            {
                hr = corDebug.TryCreateProcess(
                    lpApplicationName: null,
                    lpCommandLine: options.CommandLine,
                    lpProcessAttributes: default,
                    lpThreadAttributes: default,
                    bInheritHandles: true,
                    dwCreationFlags: creationFlags,
                    lpEnvironment: (IntPtr) pEnvironment,
                    lpCurrentDirectory: null,
                    si,
                    ref pi,
                    CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS,
                    out corDebugProcess
                );
            }

            Log.Debug<CordbProcess>("Launched process {debuggeePid}", pi.dwProcessId);
            LogNativeCorDebugProcessInfo(corDebugProcess);

            ValidatePostCreateOrAttach(hr, $"launch process '{options.CommandLine}'");

            try
            {
                processId = pi.dwProcessId;
                is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

                RegisterCallbacks();
                StoreSessionInfo(null);

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

            wait?.Set();
        }

        protected override void AttachInternal()
        {
            //Initialize is automatically called
            corDebug = new CorDebug();
            LogNativeCorDebugInfo(corDebug);

            AttachCommon(false);
        }
    }
}
