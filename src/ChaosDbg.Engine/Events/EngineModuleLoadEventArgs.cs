using System;
using ChaosDbg.Cordb;

namespace ChaosDbg
{
    public class EngineModuleLoadEventArgs : EventArgs
    {
        public IDbgModule Module { get; }

        /// <summary>
        /// Gets an optional context value that may be associated with the module load event based on the owning debugger.<para/>
        /// For <see cref="CordbModule"/> items, this will be a <see cref="bool"/> indicating whether the event was Out-Of-Band or not.
        /// </summary>
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
