using System.Collections.Generic;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbModuleStore
    {
        private Dictionary<CORDB_ADDRESS, CordbModule> modules = new Dictionary<CORDB_ADDRESS, CordbModule>();

        internal CordbModule Add(CorDebugModule corDebugModule)
        {
            var module = new CordbModule(corDebugModule);

            modules.Add(corDebugModule.BaseAddress, module);

            return module;
        }

        internal CordbModule Remove(CORDB_ADDRESS baseAddress)
        {
            if (modules.TryGetValue(baseAddress, out var module))
                modules.Remove(module.BaseAddress);

            return module;
        }
    }
}
