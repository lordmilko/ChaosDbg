using System;
using System.Runtime.InteropServices;

namespace TestApp
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    static class NativeMethods
    {
        private const string user32 = "user32.dll";

        [DllImport(user32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    }
}
