using System.Diagnostics;
using System.IO;
using ChaosDbg.Symbol;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Extends the capabilities of <see cref="DebuggerSymbolProvider"/> based on the state known to a <see cref="CordbProcess"/>.
    /// </summary>
    class CordbDebuggerSymbolProviderExtension : IDebuggerSymbolProviderExtension
    {
        private CordbProcess process;

        public CordbDebuggerSymbolProviderExtension(CordbProcess process)
        {
            this.process = process;
        }

        public bool TryGetSOS(out SOSDacInterface sos, out ProcessModule clr)
        {
            if (process.Session.IsCLRLoaded)
            {
                sos = process.DAC.SOS;

                //Querying for processes is slow, so cache the CLR process module so we don't have to look for it again in the symbol provider
                clr = process.DAC.CLR;
                return true;
            }

            sos = default;
            clr = default;
            return false;
        }

        public bool TryNativeSymFromAddr(long address, out IDisplacedSymbol symbol)
        {
            /* Because we don't receive notification events for module unloads, it's not exactly safe to load every single module we try and find symbols for. Also, we expect most of our modules will be managed, so we don't want to waste
             * a bunch of time trying to locate symbols for modules that might not even have them. As such, we only try and load native symbols for the CLR. In non-interop mode, it's assumed that a process is purely managed, and when the CLR goes
             * away so does our debugging session */

            if (!process.Session.IsInterop)
            {
                //When not interop debugging, we won't have access to native symbols for any modules (besides the CLR,
                //which may force load into DbgHelp). As such, if we see an address is located inside one of our modules,
                //and if so create a missing symbol so we can at least say something about this code location

                if (process.Modules.TryGetModuleForAddress(address, out var module))
                {
                    var name = Path.GetFileNameWithoutExtension(module.Name);
                    var displacement = address - module.BaseAddress;

                    //The address contains the displacement in it already, so we dont need to adjust it
                    symbol = new DisplacedMissingSymbol(displacement, name, address);
                    return true;
                }
            }

            //No match for our managed modules, or we're interop debugging and want to fallback to DbgHelp
            symbol = default;
            return false;
        }
    }
}
