using System;
using ChaosDbg.DbgEng;

namespace ChaosDbg
{
    public class EngineModuleLoadEventArgs : EventArgs
    {
        public DbgEngModule Module { get; }

        public EngineModuleLoadEventArgs(DbgEngModule module)
        {
            Module = module;
        }
    }
}
