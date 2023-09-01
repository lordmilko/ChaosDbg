using System;
using ChaosDbg.DbgEng;

namespace ChaosDbg
{
    public class EngineModuleUnloadEventArgs : EventArgs
    {
        public DbgEngModule Module { get; }

        public EngineModuleUnloadEventArgs(DbgEngModule module)
        {
            Module = module;
        }
    }
}
