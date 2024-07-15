using System;
using System.Runtime.InteropServices;

namespace TestApp
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    static class NativeMethods
    {
        private const string user32 = "user32.dll";
        private const string kernel32 = "kernel32.dll";

        [DllImport(user32, SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport(kernel32, SetLastError = true)]
        public static extern int TlsAlloc();

        [DllImport(kernel32, SetLastError = true)]
        public static extern bool TlsSetValue(
            [In] int dwTlsIndex,
            [In] IntPtr lpTlsValue);
    }
}
