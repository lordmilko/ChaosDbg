using System;
using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg.Symbol
{
    /// <summary>
    /// Provides facilities for extending the capabilities of a <see cref="DebuggerSymbolProvider"/>.
    /// </summary>
    interface IDebuggerSymbolProviderExtension
    {
        bool TryGetSOS(out SOSDacInterface sos, out ProcessModule clr);

        HRESULT TryReadVirtual(long address, IntPtr buffer, int size, out int read);

        void WriteVirtual<T>(long address, T value) where T : struct;
    }
}
