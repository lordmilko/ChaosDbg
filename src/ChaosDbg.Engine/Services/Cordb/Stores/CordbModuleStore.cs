using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a store used to manage and provide access to the native and managed modules that have been loaded into the current process.
    /// </summary>
    public class CordbModuleStore : IEnumerable<CordbModule>
    {
        private object moduleLock = new object();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> managedModules = new Dictionary<CORDB_ADDRESS, CordbManagedModule>();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> ManagedModules
        {
            get
            {
                if (!process.IsV3)
                    return managedModules;

                return process.CorDebugProcess.AppDomains.SelectMany(a => a.Assemblies).SelectMany(a => a.Modules)
                    .ToDictionary(m => m.BaseAddress, m => new CordbManagedModule(m, process, null));
            }
        }

        private Dictionary<CORDB_ADDRESS, CordbNativeModule> nativeModules = new Dictionary<CORDB_ADDRESS, CordbNativeModule>();
        private Dictionary<ICorDebugProcess, CordbManagedProcessPseudoModule> managedProcessModules = new Dictionary<ICorDebugProcess, CordbManagedProcessPseudoModule>();
        private Dictionary<int, CordbNativeModule> nativeProcessModules = new Dictionary<int, CordbNativeModule>();

        private CordbProcess process;

        public CordbModuleStore(CordbProcess process)
        {
            //We don't need to worry about attach; we'll receive fake module load messages for both
            //native and managed modules

            this.process = process;
        }

        /// <summary>
        /// Adds a managed module to the module store.
        /// </summary>
        /// <param name="corDebugModule">The managed module to add.</param>
        /// <returns>A <see cref="CordbManagedModule"/> that encapsulates the raw <see cref="CorDebugModule"/>.</returns>
        internal CordbManagedModule Add(CorDebugModule corDebugModule)
        {
            //This method is extremely performance critical. Any slowdowns in this thread will have a drastic effect on the performance of the process,
            //due to the sheer number of modules it may have to load.

            PEFile peFile = null;
            var preferNativePE = false;
            RelativeToAbsoluteStream stream = null;
            var needFullPELoad = false;

            if (!corDebugModule.IsDynamic)
            {
                stream = CordbMemoryStream.CreateRelative(process.DataTarget, corDebugModule.BaseAddress);

                if (process.Session.IsInterop)
                    preferNativePE = true;
                else
                {
                    //Defer querying the non-essential directories until we access the CordbModule.PEFile.
                    //We'll also try and asynchronously eagerly load them on the Symbol Resolution Thread
                    peFile = process.Session.Services.PEFileProvider.ReadStream(stream, true);
                    needFullPELoad = true;
                }
            }

            CordbManagedModule module;

            lock (moduleLock)
            {
                CordbNativeModule native = null;

                if (process.Session.IsInterop)
                {
                    if (nativeModules.TryGetValue(corDebugModule.BaseAddress, out native))
                    {
                        if (native.ManagedModule != null)
                            throw new InvalidOperationException($"Cannot set managed module: native module '{native}' already has a native module associated with it.");
                    }
                }

                if (preferNativePE)
                {
                    if (native != null)
                    {
                        //Don't ask for the public PEFile, as that will force symbol resolution which we're not ready for
                        peFile = native.GetRawPEFile();
                    }
                    else
                    {
                        //Uh oh, need to resolve the PEFile under the lock!
                        peFile = process.Session.Services.PEFileProvider.ReadStream(stream, true);
                        needFullPELoad = true;
                    }
                }

                module = new CordbManagedModule(corDebugModule, process, peFile);

                if (native != null)
                {
                    native.ManagedModule = module;
                    module.NativeModule = native;
                }

                ManagedModules.Add(corDebugModule.BaseAddress, module);
            }

            //We don't need to add a managed module to our symbol store; we only resolve managed symbols when we see them

            process.Assemblies.LinkModule(module);

            if (needFullPELoad)
                process.Symbols.DeferCalculateModuleInfoAsync(module.Name, corDebugModule.BaseAddress, peFile, resolvePESymbols: false);

            return module;
        }

        /// <summary>
        /// Adds a native module to the module store.
        /// </summary>
        /// <param name="loadDll">The native module to add.</param>
        /// <returns>A <see cref="CordbNativeModule"/> that represents the native module.</returns>
        internal unsafe CordbNativeModule Add(in LOAD_DLL_DEBUG_INFO loadDll)
        {
            //This method is extremely performance critical. Any slowdowns in this thread will have a drastic effect on the performance of the process,
            //due to the sheer number of modules it may have to load.

            var baseAddress = (long) (void*) loadDll.lpBaseOfDll;

            //Unfortunately, the LOAD_DLL_DEBUG_INFO does not tell us the size of the image, so we're forced to read the bare minimum from memory
            var memoryStream = CordbMemoryStream.CreateRelative(process.DataTarget, baseAddress);

            //We'll need to defer querying for the rest of the PEFile until later (inside CordbModule.PEFile), as PESymbolResolver will need to use symbols
            //in order to resolve any exception handlers
            var peFile = process.Session.Services.PEFileProvider.ReadStream(memoryStream, true);

            var name = CordbNativeModule.GetNativeModuleName(loadDll);

            CordbNativeModule native;

            lock (moduleLock)
            {
                //The only way to get the image size is to read the PE header; DbgEng does that too
                //Symbol resolution is deferred to the Symbol Resolution Thread, which will try and access
                //CordbNativeModule.SymbolModule
                native = new CordbNativeModule(name, baseAddress, process, peFile);

                nativeModules.Add(baseAddress, native);

                if (ManagedModules.TryGetValue(baseAddress, out var managed))
                {
                    if (managed.NativeModule != null)
                        throw new InvalidOperationException($"Cannot set native module: managed module '{managed}' already has a native module associated with it.");

                    native.ManagedModule = managed;
                    managed.NativeModule = native;
                }
            }

            process.Symbols.DeferCalculateModuleInfoAsync(name, native.BaseAddress, peFile, resolvePESymbols: true);

            return native;
        }

        /// <summary>
        /// Adds a managed process to the module store.<para/>
        /// <see cref="CorDebugProcess"/> objects so not provide any information about the modules that underpin them, so this method queries the operating system directly
        /// to retrieve the executable's module details.
        /// </summary>
        /// <param name="corDebugProcess">The process to add.</param>
        /// <returns>A <see cref="CordbManagedProcessPseudoModule"/> that represents the process' module.</returns>
        internal unsafe CordbManagedProcessPseudoModule Add(CorDebugProcess corDebugProcess)
        {
            var win32Process = Process.GetProcessById(corDebugProcess.Id);
            var address = (long) (void*) win32Process.MainModule.BaseAddress;
            var stream = CordbMemoryStream.CreateRelative(process.DataTarget, address);

            var peFile = process.Session.Services.PEFileProvider.ReadStream(stream, true, PEFileDirectoryFlags.All);

            //There is no CorDebugModule for a process, so we have to fake it
            var module = new CordbManagedProcessPseudoModule(win32Process.MainModule.FileName, corDebugProcess, process, peFile);

            lock (moduleLock)
            {
                managedProcessModules.Add(corDebugProcess.Raw, module);

                if (process.Session.IsInterop)
                {
                    if (nativeModules.TryGetValue(module.BaseAddress, out var native))
                    {
                        if (native.ManagedModule != null)
                            throw new InvalidOperationException($"Cannot set managed module: native module '{native}' already has a native module associated with it.");

                        module.NativeModule = native;
                    }
                }

                return module;
            }
        }

        /// <summary>
        /// Removes a managed module from the module store.
        /// </summary>
        /// <param name="baseAddress">The base address of the managed module to remove.</param>
        /// <returns>The <see cref="CordbManagedModule"/> that was removed.</returns>
        internal CordbManagedModule Remove(CORDB_ADDRESS baseAddress)
        {
            lock (moduleLock)
            {
                if (ManagedModules.TryGetValue(baseAddress, out var module))
                {
                    ManagedModules.Remove(module.BaseAddress);

                    if (process.Session.IsInterop)
                    {
                        if (nativeModules.TryGetValue(baseAddress, out var native))
                        {
                            native.ManagedModule = null;
                            module.NativeModule = null;
                        }
                    }

                    process.Assemblies.UnlinkModule(module);
                }

                return module;
            }
        }

        /// <summary>
        /// Removes a native module from the module store.
        /// </summary>
        /// <param name="unloadDll">The native module to remove.</param>
        /// <returns>The <see cref="CordbNativeModule"/> that was removed.</returns>
        internal unsafe CordbNativeModule Remove(in UNLOAD_DLL_DEBUG_INFO unloadDll)
        {
            lock (moduleLock)
                return RemoveNativeNoLock(unloadDll.lpBaseOfDll);
        }

        private CordbNativeModule RemoveNativeNoLock(CORDB_ADDRESS baseAddress)
        {
            if (nativeModules.TryGetValue(baseAddress, out var native))
            {
                process.Symbols.RemoveNativeModule(baseAddress);
                nativeModules.Remove(baseAddress);

                if (ManagedModules.TryGetValue(baseAddress, out var managed))
                {
                    managed.NativeModule = null;
                    native.ManagedModule = null;
                }
            }

            return native;
        }

        /// <summary>
        /// Removes a managed process from the module store.
        /// </summary>
        /// <param name="corDebugProcess">The process to remove.</param>
        /// <returns>The <see cref="CordbManagedProcessPseudoModule"/> that was removed.</returns>
        internal CordbManagedProcessPseudoModule Remove(CorDebugProcess corDebugProcess)
        {
            lock (moduleLock)
            {
                if (managedProcessModules.TryGetValue(corDebugProcess.Raw, out var module))
                {
                    //The native module doesn't link back to us
                    module.NativeModule = null;
                }

                return module;
            }
        }

        internal CordbNativeModule RemoveProcessModule(int processId)
        {
            lock (moduleLock)
            {
                if (nativeProcessModules.TryGetValue(processId, out var module))
                {
                    nativeProcessModules.Remove(processId);

                    return RemoveNativeNoLock(module.BaseAddress);
                }
            }

            return null;
        }

        internal CordbManagedModule GetModule(CorDebugModule corDebugModule)
        {
            lock (moduleLock)
            {
                var match = ManagedModules[corDebugModule.BaseAddress];

                return match;
            }
        }

        internal CordbModule GetModuleForAddress(long address)
        {
            if (TryGetModuleForAddress(address, out var module))
                return module;

            //Note: this method is used in contexts here it's assumed we MUST have a return type. If we modify this method to return null,
            //we should update the description in CordbFrame.Module which says you only have a null module when it's a dynamically generated module

            throw new InvalidOperationException($"Could not find which module address 0x{address:X} belongs to");
        }

        internal bool TryGetModuleForAddress(long address, out CordbModule module)
        {
            if (address == 0)
                throw new ArgumentException("Address cannot be 0", nameof(address));

            lock (moduleLock)
            {
                foreach (var item in nativeModules.Values)
                {
                    if (address >= item.BaseAddress && address <= item.EndAddress)
                    {
                        module = item;
                        return true;
                    }
                }

                foreach (var item in ManagedModules.Values)
                {
                    if (address >= item.BaseAddress && address <= item.EndAddress)
                    {
                        module = item;
                        return true;
                    }
                }

                foreach (var item in managedProcessModules.Values)
                {
                    if (address >= item.BaseAddress && address <= item.EndAddress)
                    {
                        module = item;
                        return true;
                    }
                }
            }

            module = null;
            return false;
        }

        internal void SetAsProcess(CORDB_ADDRESS lpBaseOfImage, int processId)
        {
            lock (moduleLock)
            {
                var module = nativeModules[lpBaseOfImage];

                nativeProcessModules.Add(processId, module);
            }
        }

        public IEnumerator<CordbModule> GetEnumerator()
        {
            lock (moduleLock)
            {
                return ManagedModules.Values
                    .Cast<CordbModule>()
                    .Concat(nativeModules.Values.Cast<CordbModule>())
                    .ToList()
                    .GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
