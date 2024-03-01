using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbAssemblyStore : IEnumerable<CordbAssembly>
    {
        private object assemblyLock = new object();

        //As long as we hold a reference to an RCW, its reference count wont drop to 0, ensuring we're able to maintain
        //referential identity between the COM object in this dictionary and any new COM objects that come from ICorDebug
        private Dictionary<ICorDebugAssembly, CordbAssembly> assemblies = new Dictionary<ICorDebugAssembly, CordbAssembly>();

        private CordbProcess process;

        public CordbAssemblyStore(CordbProcess process)
        {
            this.process = process;
        }

        public CordbAssembly Add(CorDebugAssembly corDebugAssembly)
        {
            var assembly = new CordbAssembly(corDebugAssembly, process);

            lock (assemblyLock)
                assemblies.Add(corDebugAssembly.Raw, assembly);

            process.AppDomains.LinkAssembly(assembly);

            return assembly;
        }

        public CordbAssembly Remove(CorDebugAssembly corDebugAssembly)
        {
            lock (assemblyLock)
            {
                if (assemblies.TryGetValue(corDebugAssembly.Raw, out var assembly))
                {
                    assemblies.Remove(corDebugAssembly.Raw);

                    process.AppDomains.UnlinkAssembly(assembly);

                    if (assembly.Module != null)
                        assembly.Module.Assembly = null;
                }

                return assembly;
            }
        }

        public void LinkModule(CordbManagedModule module)
        {
            var assembly = GetAssembly(module.CorDebugModule.Assembly);

            if (assembly.Module != null)
                throw new NotImplementedException($"Cannot link module {module} with assembly {assembly}: assembly is already linked with module {assembly.Module}. Linking multiple modules with an assembly is not implemented");

            module.Assembly = assembly;
            assembly.Module = module;
        }

        public void UnlinkModule(CordbManagedModule module)
        {
            if (module.Assembly != null)
            {
                module.Assembly.Module = null;
                module.Assembly = null;
            }
            else
            {
                lock (assemblyLock)
                {
                    if (assemblies.TryGetValue(module.CorDebugModule.Assembly.Raw, out var assembly))
                        assembly.Module = null;
                }
            }
        }

        internal CordbAssembly GetAssembly(CorDebugAssembly corDebugAssembly)
        {
            lock (assemblyLock)
            {
                if (!assemblies.TryGetValue(corDebugAssembly.Raw, out var assembly))
                    throw new InvalidOperationException($"Could not find the existing assembly that COM object '{corDebugAssembly}' corresponds to.");

                return assembly;
            }
        }

        public IEnumerator<CordbAssembly> GetEnumerator()
        {
            lock (assemblyLock)
                return assemblies.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
