using System;
using ChaosLib;

namespace ChaosDbg
{
    public interface INativeStackWalkerFunctionTableProvider
    {
        PSYMBOL_FUNCENTRY_CALLBACK64 FunctionEntryCallback { get; set; }

        IntPtr GetFunctionTableEntry(long address);

        bool TryGetNativeModuleBase(long address, out long moduleBase);
    }
}
