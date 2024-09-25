using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.Symbols;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace ChaosDbg.Hook
{
    public delegate IntPtr LoadLibraryExWDelegate(
        [In, MarshalAs(UnmanagedType.LPWStr)] string lpLibFileName, [In] IntPtr hFile, [In] LoadLibraryFlags dwFlags);

    class LoadLibraryExWConditionalHook : ConditionalHook<LoadLibraryExWDelegate>
    {
        
    }
}
