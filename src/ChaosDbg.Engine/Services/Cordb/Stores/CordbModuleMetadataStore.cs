using System;
using System.Linq;
using System.Reflection;
using ClrDebug;
using SymHelp.Metadata;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for resolving and caching <see cref="MetadataModule"/> instances.<para/>
    /// This type is the entry point to resolving ECMA-335 metadata information.
    /// </summary>
    public class CordbModuleMetadataStore : ModuleMetadataStore
    {
        private CordbModuleStore moduleStore;

        public CordbModuleMetadataStore(CordbModuleStore modules)
        {
            moduleStore = modules;
        }

        public MetadataModule GetOrAddModule(CordbManagedModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            //See if we already have an assembly with this module's AssemblyName.
            //If so, check that the manifest module of that assembly is the same as ours
            //(we currently don't support having multiple modules per assembly). If so,
            //return that assembly's existing module. Otherwise, create a new assembly

            var mdai = module.CorDebugModule.GetMetaDataInterface<MetaDataAssemblyImport>();

            var assemblyName = GetAssemblyName(mdai, module.Name);

            lock (assemblyNameMap)
            {
                if (assemblyNameMap.TryGetValue(assemblyName, out var existingAssembly))
                {
                    if (ReferenceEquals(module, ((MetadataModule) existingAssembly.ManifestModule).UnderlyingModule))
                        return (MetadataModule) existingAssembly.ManifestModule;

                    throw new InvalidOperationException($"Cannot add module '{module}' with assembly name '{assemblyName}' as an existing assembly with that name is in use by module {existingAssembly.ManifestModule}");
                }

                //It's a brand new assembly!
                var metadataAssembly = new MetadataAssembly(module.FullName, mdai, this, module); //todo: what will the fullname be for dynamic assemblies? we dont want to use just the name here!

                assemblyNameMap[assemblyName] = metadataAssembly;

                return (MetadataModule) metadataAssembly.ManifestModule;
            } 
        }

        protected override MetadataAssembly ResolveAssembly(AssemblyName assemblyName, MetadataModule sourceModule)
        {
            lock (assemblyLock)
            {
                if (assemblyNameMap.TryGetValue(assemblyName, out var metadataAssembly))
                    return metadataAssembly;

                //It's a new assembly we haven't seen before

                foreach (var module in moduleStore.OfType<CordbManagedModule>())
                {
                    var targetName = module.Assembly.AssemblyName;

                    if (AssemblyNameComparer.Instance.Equals(targetName, assemblyName))
                    {
                        //todo: what will the fullname be for dynamic assemblies? we dont want to use just the name here!
                        metadataAssembly = new MetadataAssembly(module.FullName, module.CorDebugModule.GetMetaDataInterface<MetaDataAssemblyImport>(), this, module);
                        assemblyNameMap[assemblyName] = metadataAssembly;

                        return metadataAssembly;
                    }
                }
            }
        }

        protected override MetadataAssembly ResolveSystemAssembly()
        {
            var systemLibName = moduleStore.Process.Session.IsCoreCLR ? "System.Private.CoreLib" : "mscorlib";

            foreach (var module in moduleStore.OfType<CordbManagedModule>())
            {
                if (module.Name == systemLibName)
                {
                    lock (assemblyLock)
                    {
                        if (assemblyNameMap.TryGetValue(module.Assembly.AssemblyName, out var metadataAssembly))
                            return metadataAssembly;

                        //todo: what will the fullname be for dynamic assemblies? we dont want to use just the name here!
                        metadataAssembly = new MetadataAssembly(module.FullName, module.CorDebugModule.GetMetaDataInterface<MetaDataAssemblyImport>(), this, module);
                        assemblyNameMap[module.Assembly.AssemblyName] = metadataAssembly;

                        return metadataAssembly;
                    }
                }
            }
    }
}
