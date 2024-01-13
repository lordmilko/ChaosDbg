using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbModuleStore : IEnumerable<CordbModule>
    {
        private object moduleLock = new object();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> managedModules = new Dictionary<CORDB_ADDRESS, CordbManagedModule>();
        private Dictionary<CORDB_ADDRESS, CordbNativeModule> nativeModules = new Dictionary<CORDB_ADDRESS, CordbNativeModule>();

        private CordbProcess process;

        public CordbModuleStore(CordbProcess process)
        {
            //We don't need to worry about attach; we'll receive fake module load messages for both
            //native and managed modules

            this.process = process;
        }

        internal CordbModule Add(CorDebugModule corDebugModule)
        {
            IPEFile peFile = null;

            if (!corDebugModule.IsDynamic)
            {
                var stream = CordbMemoryStream.CreateRelative(process.DAC.DataTarget, corDebugModule.BaseAddress);
                peFile = process.Session.Services.PEFileProvider.ReadStream(stream, true);
            }

            lock (moduleLock)
            {
                var module = new CordbManagedModule(corDebugModule, process, peFile);

                managedModules.Add(corDebugModule.BaseAddress, module);

                if (process.Session.IsInterop)
                {
                    if (nativeModules.TryGetValue(corDebugModule.BaseAddress, out var native))
                    {
                        if (native.ManagedModule != null)
                            throw new InvalidOperationException($"Cannot set managed module: native module '{native}' already has a native module associated with it.");

                        native.ManagedModule = module;
                        module.NativeModule = native;
                    }
                }

                return module;
            }
        }

        internal unsafe CordbNativeModule Add(in LOAD_DLL_DEBUG_INFO loadDll)
        {
            var baseAddress = (long) (void*) loadDll.lpBaseOfDll;

            var stream = CordbMemoryStream.CreateRelative(process.DAC.DataTarget, baseAddress);
            var peFile = process.Session.Services.PEFileProvider.ReadStream(stream, true);

            var name = CordbNativeModule.GetNativeModuleName(loadDll);

            process.DbgHelp.AddModule(name, baseAddress, peFile.OptionalHeader.SizeOfImage);

            lock (moduleLock)
            {
                //The only way to get the image size is to read the PE header;
                //DbgEng does that too

                var native = new CordbNativeModule(name, baseAddress, process, peFile);

                nativeModules.Add(baseAddress, native);

                if (managedModules.TryGetValue(baseAddress, out var managed))
                {
                    if (managed.NativeModule != null)
                        throw new InvalidOperationException($"Cannot set native module: managed module '{managed}' already has a native module associated with it.");

                    native.ManagedModule = managed;
                    managed.NativeModule = native;
                }

                return native;
            }
        }

        internal CordbManagedModule Remove(CORDB_ADDRESS baseAddress)
        {
            lock (moduleLock)
            {
                if (managedModules.TryGetValue(baseAddress, out var module))
                {
                    managedModules.Remove(module.BaseAddress);

                    if (process.Session.IsInterop)
                    {
                        if (nativeModules.TryGetValue(baseAddress, out var native))
                        {
                            native.ManagedModule = null;
                            module.NativeModule = null;
                        }
                    }
                }

                return module;
            }
        }

        internal unsafe CordbNativeModule Remove(in UNLOAD_DLL_DEBUG_INFO unloadDll)
        {
            lock (moduleLock)
            {
                if (nativeModules.TryGetValue(unloadDll.lpBaseOfDll, out var native))
                {
                    process.DbgHelp.RemoveModule((long) (void*) unloadDll.lpBaseOfDll);
                    nativeModules.Remove(unloadDll.lpBaseOfDll);

                    if (managedModules.TryGetValue(unloadDll.lpBaseOfDll, out var managed))
                    {
                        managed.NativeModule = null;
                        native.ManagedModule = null;
                    }
                }

                return native;
            }
        }

        internal CordbManagedModule GetModule(CorDebugModule corDebugModule)
        {
            lock (moduleLock)
            {
                var match = managedModules[corDebugModule.BaseAddress];

                return match;
            }
        }

        internal CordbModule GetModule(long ip)
        {
            lock (moduleLock)
            {
                foreach (var item in nativeModules.Values)
                {
                    if (ip >= item.BaseAddress && ip <= item.EndAddress)
                        return item;
                }
            }

            //Note: this method is used in contexts here it's assumed we MUST have a return type. If we modify this method to return null,
            //we should update the description in CordbFrame.Module which says you only have a null module when it's a dynamically generated module

            throw new InvalidOperationException($"Could not find which module address 0x{ip:X} belongs to");
        }

        public IEnumerator<CordbModule> GetEnumerator()
        {
            lock (moduleLock)
            {
                return managedModules.Values
                    .Cast<CordbModule>()
                    .Concat(nativeModules.Values.Cast<CordbModule>())
                    .ToList()
                    .GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
