using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbModuleStore : IEnumerable<CordbModule>
    {
        private object moduleLock = new object();

        private Dictionary<CORDB_ADDRESS, CordbModule> modules = new Dictionary<CORDB_ADDRESS, CordbModule>();

        private CordbProcess process;

        public CordbModuleStore(CordbProcess process)
        {
            this.process = process;
        }

        internal CordbModule Add(CorDebugModule corDebugModule)
        {
            lock (moduleLock)
            {
                var module = new CordbModule(corDebugModule);

                modules.Add(corDebugModule.BaseAddress, module);

                return module;
            }
        }

        internal CordbModule Remove(CORDB_ADDRESS baseAddress)
        {
            lock (moduleLock)
            {
                if (modules.TryGetValue(baseAddress, out var module))
                {
                    modules.Remove(module.BaseAddress);
                }

                return module;
            }
        }

        public IEnumerator<CordbModule> GetEnumerator()
        {
            lock (moduleLock)
            {
                return modules.Values.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
