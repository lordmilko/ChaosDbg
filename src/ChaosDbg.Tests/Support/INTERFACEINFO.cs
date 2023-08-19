﻿using System;
using System.Runtime.InteropServices;

namespace ChaosDbg
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct INTERFACEINFO
    {
        [MarshalAs(UnmanagedType.IUnknown)]
        public object punk;

        public Guid iid;

        public ushort wMethod;
    }
}
