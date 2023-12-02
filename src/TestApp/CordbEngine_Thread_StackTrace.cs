using System;
using System.Runtime.CompilerServices;

namespace TestApp
{
    class CordbEngine_Thread_StackTrace
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Managed() => Program.SignalReady();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Internal()
        {
            EnumWindowsProc proc = (a, b) =>
            {
                Program.SignalReady();
                return false;
            };

            NativeMethods.EnumWindows(proc, IntPtr.Zero);
            GC.KeepAlive(proc);
        }
    }
}
