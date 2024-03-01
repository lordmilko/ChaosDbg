using System;

namespace ChaosDbg
{
    public class EngineModuleLoadEventArgs : EventArgs
    {
        public IDbgModule Module { get; }

        public object UserContext { get; }

        public EngineModuleLoadEventArgs(IDbgModule module, object userContext = null)
        {
            Module = module;
            UserContext = userContext;
        }

        public override string ToString()
        {
            return Module.ToString();
        }
    }
}
