using System;

namespace ChaosDbg
{
    public class EngineModuleUnloadEventArgs : EventArgs
    {
        public IDbgModule Module { get; }

        public EngineModuleUnloadEventArgs(IDbgModule module)
        {
            Module = module;
        }
    }
}
