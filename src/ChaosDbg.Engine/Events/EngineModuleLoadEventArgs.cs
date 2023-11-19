using System;

namespace ChaosDbg
{
    public class EngineModuleLoadEventArgs : EventArgs
    {
        public IDbgModule Module { get; }

        public EngineModuleLoadEventArgs(IDbgModule module)
        {
            Module = module;
        }
    }
}
