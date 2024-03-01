using System;

namespace ChaosDbg
{
    public class EngineModuleUnloadEventArgs : EventArgs
    {
        public IDbgModule Module { get; }

        public object UserContext { get; }

        public EngineModuleUnloadEventArgs(IDbgModule module, object userContext = null)
        {
            Module = module;
            UserContext = userContext;
        }
    }
}
