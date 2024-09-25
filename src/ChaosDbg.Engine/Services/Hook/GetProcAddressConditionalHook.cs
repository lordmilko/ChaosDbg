using System;
using System.Runtime.InteropServices;
using ChaosLib;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace ChaosDbg.Hook
{
    public delegate IntPtr GetProcAddressDelegate(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string name);

    class GetProcAddressConditionalHook : ConditionalHook<GetProcAddressDelegate>
    {
        
    }
}
