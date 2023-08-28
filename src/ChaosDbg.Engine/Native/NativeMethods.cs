using System;
using System.Runtime.InteropServices;
using ClrDebug;

namespace ChaosDbg
{
    internal static class NativeMethods
    {
        private const string kernel32 = "kernel32.dll";

        public const int INFINITE = -1;

        #region Kernel32

        [DllImport(kernel32, SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport(kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessW(
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            CreateProcessFlags dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOW lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport(kernel32, SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hLibModule);

        [DllImport(kernel32, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport(kernel32, SetLastError = true)]
        public static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out, MarshalAs(UnmanagedType.Bool)] out bool Wow64Process);

        [DllImport(kernel32, CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryW")]
        public static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport(kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        #endregion
    }
}
