using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Stores modules that have been loaded into a process under a DbgEng debugger.
    /// </summary>
    public class DbgEngModuleStore : IEnumerable<DbgEngModule>
    {
        private object moduleLock = new object();

        private Dictionary<long, DbgEngModule> modules = new Dictionary<long, DbgEngModule>();

        internal DbgEngModule Add(long baseAddress, string imageName, string moduleName, int moduleSize)
        {
            lock (moduleLock)
            {
                var module = new DbgEngModule(baseAddress, imageName, moduleName, moduleSize);

                //If somehow the same module is loaded at the same address multiple times without being unloaded, this may potentially
                //indicate a bug and we'd like this to explode
                modules.Add(baseAddress, module);

                return module;
            }
        }

        internal DbgEngModule Remove(long baseAddress)
        {
            lock (moduleLock)
            {
                if (modules.TryGetValue(baseAddress, out var module))
                    modules.Remove(module.BaseAddress);

                return module;
            }
        }

        internal DbgEngModule GetModuleForAddress(long address)
        {
            lock (moduleLock)
            {
                foreach (var item in modules.Values)
                {
                    if (address >= item.BaseAddress && address <= item.EndAddress)
                        return item;
                }
            }

            throw new InvalidOperationException($"Could not find a valid module that contains address 0x{address:X}");
        }

        public IEnumerator<DbgEngModule> GetEnumerator()
        {
            lock (moduleLock)
            {
                return modules.Values.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
