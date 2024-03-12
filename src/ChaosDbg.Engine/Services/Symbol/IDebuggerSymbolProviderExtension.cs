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
    }
}
