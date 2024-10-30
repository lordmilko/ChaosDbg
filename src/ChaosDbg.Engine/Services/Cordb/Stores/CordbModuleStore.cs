using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChaosLib;
using ChaosLib.Memory;
using ClrDebug;
using PESpy;
using Win32Process = System.Diagnostics.Process;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a store used to manage and provide access to the native and managed modules that have been loaded into the current process.
    /// </summary>
    public class CordbModuleStore : IDbgModuleStoreInternal, IEnumerable<CordbModule>
    {
        private object moduleLock = new object();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> managedModules = new Dictionary<CORDB_ADDRESS, CordbManagedModule>();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> ManagedModules
        {
            get
            {
                if (!Process.IsV3)
                    return managedModules;

                return Process.CorDebugProcess.AppDomains.SelectMany(a => a.Assemblies).SelectMany(a => a.Modules)
                    .ToDictionary(m => m.BaseAddress, m => new CordbManagedModule(m, Process, null));
            }
        }

        public CordbModuleMetadataStore MetadataStore { get; }

        private Dictionary<CORDB_ADDRESS, CordbNativeModule> nativeModules = new Dictionary<CORDB_ADDRESS, CordbNativeModule>();
        private Dictionary<ICorDebugProcess, CordbManagedProcessPseudoModule> managedProcessModules = new Dictionary<ICorDebugProcess, CordbManagedProcessPseudoModule>();
        private Dictionary<int, CordbNativeModule> nativeProcessModules = new Dictionary<int, CordbNativeModule>();

        internal CordbProcess Process { get; }

        public CordbModuleStore(CordbProcess process)
        {
            //We don't need to worry about attach; we'll receive fake module load messages for both
            //native and managed modules

            Process = process;
            MetadataStore = new CordbModuleMetadataStore(this);
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

            var baseAddress = corDebugModule.BaseAddress;

            if (!corDebugModule.IsDynamic)
            {
                stream = MemoryReaderStream.CreateRelative((IMemoryReader) Process.DataTarget, baseAddress);

                if (Process.Session.IsInterop)
                    preferNativePE = true;
                else
                {
                    //Defer querying the non-essential directories until we access the CordbModule.PEFile.
                    //We'll also try and asynchronously eagerly load them on the Symbol Resolution Thread
                    peFile = PEFile.FromStream(stream, true, new PEFileServices(baseAddress, Process.Symbols));
                    needFullPELoad = true;
                }
            }

            CordbManagedModule module;

            lock (moduleLock)
            {
                CordbNativeModule native = null;

                if (Process.Session.IsInterop)
                {
                    if (nativeModules.TryGetValue(baseAddress, out native))
                    {
                        if (native.ManagedModule != null)
                            throw new InvalidOperationException($"Cannot set managed module: native module '{native}' already has a native module associated with it.");
                    }
                }

                if (preferNativePE)
                {
                    if (native != null)
                    {
                        peFile = native.PEFile;
                    }
                    else
                    {
                        //Uh oh, need to resolve the PEFile under the lock!
                        peFile = PEFile.FromStream(stream, true, new PEFileServices(baseAddress, Process.Symbols));
                        needFullPELoad = true;
                    }
                }

                module = new CordbManagedModule(corDebugModule, Process, peFile);

                if (native != null)
                {
                    native.ManagedModule = module;
                    module.NativeModule = native;
                }

                ManagedModules.Add(baseAddress, module);
            }

            //We don't need to add a managed module to our symbol store; we only resolve managed symbols when we see them

            Process.Assemblies.LinkModule(module);

            if (needFullPELoad)
                Process.Symbols.LoadModuleAsync(module.FullName, baseAddress, corDebugModule.Size, module, peFile);

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
            var memoryStream = MemoryReaderStream.CreateRelative((IMemoryReader) Process.DataTarget, baseAddress);

            //We'll need to defer querying for the rest of the PEFile until later (inside CordbModule.PEFile), as PESymbolResolver will need to use symbols
            //in order to resolve any exception handlers
            var peFile = PEFile.FromStream(memoryStream, true, new PEFileServices(baseAddress, Process.Symbols));

            var name = CordbNativeModule.GetNativeModuleName(loadDll, Process.Id);

            CordbNativeModule native;

            lock (moduleLock)
            {
                //The only way to get the image size is to read the PE header; DbgEng does that too
                //Symbol resolution is deferred to the Symbol Resolution Thread, which will try and access
                //CordbNativeModule.SymbolModule
                native = new CordbNativeModule(name, baseAddress, Process, peFile);

                nativeModules.Add(baseAddress, native);

                if (ManagedModules.TryGetValue(baseAddress, out var managed))
                {
                    if (managed.NativeModule != null)
                        throw new InvalidOperationException($"Cannot set native module: managed module '{managed}' already has a native module associated with it.");

                    native.ManagedModule = managed;
                    managed.NativeModule = native;
                }
            }

            //It's important to note that you cannot rely on the PEFile's ImageBase to know where a given module has been loaded. A LOAD_DLL_DEBUG_EVENT event occurs
            //whenever a process calls kernel32!MapViewOfFile with a section that created by kernel32!CreateFileMapping with SEC_IMAGE. The CLR will sometimes map two copies of
            //mscorlib, do something, and then unload one. 
            Process.Symbols.LoadModuleAsync(name, native.BaseAddress, native.Size, null, peFile);

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
            var win32Process = Win32Process.GetProcessById(corDebugProcess.Id);
            var address = (long) (void*) win32Process.MainModule.BaseAddress;
            var stream = MemoryReaderStream.CreateRelative((IMemoryReader) Process.DataTarget, address);

            var peFile = PEFile.FromStream(stream, true, new PEFileServices(address, Process.Symbols));

            //There is no CorDebugModule for a process, so we have to fake it
            var module = new CordbManagedProcessPseudoModule(win32Process.MainModule.FileName, corDebugProcess, Process, peFile);

            lock (moduleLock)
            {
                managedProcessModules.Add(corDebugProcess.Raw, module);

                if (Process.Session.IsInterop)
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
                    module.IsLoaded = false;
                    ManagedModules.Remove(module.BaseAddress);

                    if (Process.Session.IsInterop)
                    {
                        if (nativeModules.TryGetValue(baseAddress, out var native))
                        {
                            native.ManagedModule = null;
                            module.NativeModule = null;
                        }
                    }

                    Process.Assemblies.UnlinkModule(module);
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
                native.IsLoaded = false;
                Process.Symbols.UnloadModule(baseAddress);
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

        /// <summary>
        /// Removes the special <see cref="CordbNativeModule"/> that is associated with the process EXE.
        /// </summary>
        /// <param name="processId">The process ID of the executable.</param>
        /// <returns>The <see cref="CordbNativeModule"/> that was associated with the process, or <see langword="null"/> if an associated module could not be found.</returns>
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

        /// <summary>
        /// Retrieves the <see cref="CordbManagedModule"/> that is associated with the base address of the specified
        /// <see cref="CorDebugModule"/>.
        /// </summary>
        /// <param name="corDebugModule">The module to lookup.</param>
        /// <returns>The <see cref="CordbManagedModule"/> that is associated with the specified <see cref="CorDebugModule.BaseAddress"/>.</returns>
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

        IEnumerator<IDbgModule> IDbgModuleStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
