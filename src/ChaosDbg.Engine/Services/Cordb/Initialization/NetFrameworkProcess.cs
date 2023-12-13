using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    class NetFrameworkProcess : NetInitCommon
    {
        public static void Create(
            CreateProcessOptions createProcessOptions,
            InitCallback initCallback)
        {
            //Initialize is automatically called
            var corDebug = new CorDebug();

            //Setup callbacks
            var cb = new CordbManagedCallback();
            corDebug.SetManagedHandler(cb);

            CordbUnmanagedCallback ucb = null;

            if (createProcessOptions.UseInterop)
            {
                ucb = new CordbUnmanagedCallback();
                corDebug.SetUnmanagedHandler(ucb);
            }

            //Create the process

            GetCreateProcessArgs(createProcessOptions, out var creationFlags, out var si);

            if (createProcessOptions.UseInterop)
            {
                //Required for interop debugging. MSDN claims you need to do DEBUG_PROCESS | DEBUG_ONLY_THIS_PROCESS however
                //rsmain.cpp!Cordb::CreateProcessCommon says this is wrong
                creationFlags |= CreateProcessFlags.DEBUG_ONLY_THIS_PROCESS;
            }

            var pi = new PROCESS_INFORMATION();

            var hr = corDebug.TryCreateProcess(
                lpApplicationName: null,
                lpCommandLine: createProcessOptions.CommandLine,
                lpProcessAttributes: default,
                lpThreadAttributes: default,
                bInheritHandles: true,
                dwCreationFlags: creationFlags,
                IntPtr.Zero,
                lpCurrentDirectory: null,
                si,
                ref pi,
                CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS,
                out var process
            );

            ValidateCreateOrAttach(hr, $"launch process '{createProcessOptions.CommandLine}'");

            try
            {
                var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

                initCallback(
                    corDebug,
                    process,
                    cb,
                    ucb,
                    is32Bit,
                    createProcessOptions.CommandLine,
                    createProcessOptions.UseInterop
                );

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

        public static void Attach(
            AttachProcessOptions attachProcessOptions,
            InitCallback initCallback)
        {
            var pid = attachProcessOptions.ProcessId;

            //Initialize is automatically called
            var corDebug = new CorDebug();

            //Setup callbacks
            var cb = new CordbManagedCallback();
            corDebug.SetManagedHandler(cb);

            CordbUnmanagedCallback ucb = null;

            if (attachProcessOptions.UseInterop)
            {
                ucb = new CordbUnmanagedCallback();
                corDebug.SetUnmanagedHandler(ucb);
            }

            //Do the attach
            var hr = corDebug.TryDebugActiveProcess(pid, attachProcessOptions.UseInterop, out var process);

            ValidateCreateOrAttach(hr, $"attach to process {pid}");

            var is32Bit = Kernel32.IsWow64ProcessOrDefault(process.Handle);

            initCallback(
                corDebug,
                process,
                cb,
                ucb,
                is32Bit,
                null,
                attachProcessOptions.UseInterop
            );
        }

        private static void ValidateCreateOrAttach(HRESULT hr, string action)
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
    }
}
