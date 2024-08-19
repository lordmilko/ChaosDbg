using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ChaosDbg.Debugger;
using ChaosDbg.SymStore;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Symbol
{
    //Members that pertain to resolving unmanaged symbols

    public partial class DebuggerSymbolProvider
    {
        /// <summary>
        /// Stores all modules that have been successfully loaded on the SymbolThread (or the main thread if we force
        /// loaded symbols)
        /// </summary>
        /// <remarks>
        /// A concurrent dictionary is used here instead of a dictionary + lock because if we break into the debugger and the symbol resolution thread
        /// holds a lock on the dictionary, nobody will be able to resolve any symbols since touching the known modules dictionary would require taking
        /// a lock!
        /// </remarks>
        private ConcurrentDictionary<long, IUnmanagedSymbolModule> loadedNativeModules = new ConcurrentDictionary<long, IUnmanagedSymbolModule>();

        private ConcurrentDictionary<long, long> loadedModuleRanges = new ConcurrentDictionary<long, long>();

        private DispatcherPriorityQueueWorker worker;
        private SymbolDeferrableOperationContext workerContext;

        /// <summary>
        /// The underlying DbgHelp session that is used to provide access to native symbols
        /// </summary>
        private IDbgHelp dbgHelp;

        private NativeSymbolProvider nativeSymbolProvider;

        /// <summary>
        /// Gets or sets the callback that is used for unwinding call frames pertaining to dynamically generated code in x64 processes.
        /// </summary>
        public PSYMBOL_FUNCENTRY_CALLBACK64 FunctionEntryCallback
        {
            get => dbgHelp.FunctionEntryCallback;
            set => dbgHelp.FunctionEntryCallback = value;
        }

        IDbgHelp INativeStackWalkerFunctionTableProvider.UnsafeGetDbgHelp() => dbgHelp;

        /// <summary>
        /// Tries to get the module base of the native module that owns the specified memory address.
        /// </summary>
        /// <param name="address">The address to get the owning module of.</param>
        /// <param name="moduleBase">The base address of the module that owns the specified <paramref name="address"/>.</param>
        /// <returns>True if a module containing the specified address was found, otherwise false.</returns>
        public bool TryGetNativeModuleBase(long address, out long moduleBase)
        {
            //Fast path: the module has already been loaded
            if (dbgHelp.TrySymGetModuleBase64(address, out moduleBase) == S_OK)
                return true;

            //Strictly speaking, they didn't us for symbols, they just wanted to know the module base!
            //If they also want symbols, we'll force load them if they then call TrySymFromAddr
            if (TryGetModuleBase(address, out moduleBase))
                return true;

            return false;
        }

        private bool TryNativeSymFromAddr(long address, out IDisplacedSymbol result)
        {
            /* DbgHelp.SymFromAddr is just way too slow. We're constantly trying to load additional symbols on the background thread,
             * which takes our lock for communicating with DbgHelp. To mitigate this, we'll try and emulate the logic DbgHelp uses for retrieving
             * symbols from DIA ourselves. Only if this fails will we fall back to asking DbgHelp directly */

            bool hasLooped = false;
start:
            if (dbgHelp.TrySymFromAddr(address, out var raw) == S_OK)
            {
                //DbgHelp knows about the module, so we should too

                if (loadedNativeModules.TryGetValue(raw.SymbolInfo.ModuleBase, out var existingModule))
                {
                    result = new DbgHelpDisplacedSymbol(raw.Displacement, raw.SymbolInfo, new DbgHelpSymbol(raw.SymbolInfo, (DbgHelpSymbolModule) existingModule));
                    return true;
                }
                else
                {
                    if (!hasLooped && TryGetModuleBase(address, out var moduleBase))
                    {
                        EnsureModuleLoaded(moduleBase, true);

                        hasLooped = true;
                        goto start;
                    }
                }
            }
            else
            {
                if (!hasLooped && TryGetModuleBase(address, out var moduleBase))
                {
                    EnsureModuleLoaded(moduleBase, true);

                    hasLooped = true;
                    goto start;
                }
            }

            result = default;
            return false;
        }

        //If the module that would own the symbol isn't loaded yet, not much we can do
        public bool TryNativeSymFromName(string name, out SymbolInfo symbol) =>
            dbgHelp.TrySymFromName(name, out symbol) == S_OK;

        public IUnmanagedSymbol[] NativeSymEnumSymbols(string mask, Func<SymbolInfo, bool> predicate = null)
        {
            var results = new List<IUnmanagedSymbol>();

            EnsureModuleLoaded(mask);

            dbgHelp.SymEnumSymbolsEx(
                0,
                info =>
                {
                    if (!loadedNativeModules.TryGetValue(info.ModuleBase, out var module))
                        throw new InvalidOperationException($"Got a symbol in a module that isn't registered ({info.ModuleBase:X}). This should be impossible.");

                    if (predicate != null && !predicate(info))
                        return true;

                    results.Add(new DbgHelpSymbol(info, (DbgHelpSymbolModule) module));

                    return true;
                },
                mask
            );

            return results.ToArray();
        }

        public T WithFrameContext<T>(long ip, Func<DebuggerSymbolProvider, T> func)
        {
            if (TryGetModuleBase(ip, out var moduleBase))
                EnsureModuleLoaded(moduleBase, true);

            return dbgHelp.WithFrameContext(ip, () => func(this));
        }

        internal bool TryGetSymbolModule(long baseOfDll, out IUnmanagedSymbolModule symbolModule) =>
            loadedNativeModules.TryGetValue(baseOfDll, out symbolModule);

        public IUnmanagedSymbolModule GetOrAddNativeModule(string imageName, long baseOfDll, int dllSize)
        {
            if (loadedNativeModules.TryGetValue(baseOfDll, out var existing))
                return existing;

            EnsureModuleLoaded(baseOfDll, true);

            if (loadedNativeModules.TryGetValue(baseOfDll, out existing))
                return existing;

            return AddNativeModule(imageName, baseOfDll, dllSize);
        }

        #region Add

        public IUnmanagedSymbolModule AddNativeModule(string imageName, long baseOfDll, int dllSize, ModuleSymFlag flags = default)
        {
            dbgHelp.SymLoadModuleEx(imageName: imageName, baseOfDll: baseOfDll, dllSize: dllSize, flags: flags);

            var module = CreateModule(imageName, baseOfDll, dllSize);

            TryRegisterCLR(module);

            loadedNativeModules[baseOfDll] = module;

            return module;
        }

        #endregion
        #region Remove

        public void RemoveNativeModule(long baseOfDll)
        {
            if (worker.TryGetOperation(baseOfDll, out var operation))
            {
                worker.Abort(operation);
            }

            loadedModuleRanges.TryRemove(baseOfDll, out _);

            if (loadedNativeModules.TryGetValue(baseOfDll, out _))
            {
                //Apparently, as long as the key is still present, TryRemove will always succeed
                loadedNativeModules.TryRemove(baseOfDll, out _);

                dbgHelp.SymUnloadModule64(baseOfDll);
            }
        }

        #endregion
        #region Defer

        public void DeferCalculateModuleInfoAsync(string name, long baseOfDll, PEFile peFile, bool resolvePESymbols)
        {
            var isNtdll = Path.GetFileNameWithoutExtension(name).ToLower() == "ntdll";

            var op = new SymbolDeferrableOperation(
                name,
                baseOfDll,
                peFile,
                workerContext,

                new SymbolDeferrableSubOperation(isNtdll ? 0 : 1, default),
                new PEFileDeferrableSubOperation(resolvePESymbols)
            );

            loadedModuleRanges[baseOfDll] = baseOfDll + peFile.OptionalHeader.SizeOfImage;

            worker.Enqueue(op);
        }

        public ISymbolModule RegisterFullSymbolModule(string filePath, long baseOfDll, int dllSize)
        {
            //Create the module from the original file path however
            var module = CreateModule(filePath, baseOfDll, dllSize);

            //If this is the CLR (clr.dll / coreclr.dll) record the fact we've loaded symbols for it now. In non-interop debugging
            //we'll force load symbols for the CLR if we don't have them, so for interop debugging we need to record the fact we have
            //the CLR's symbols already
            TryRegisterCLR(module);

            loadedNativeModules[baseOfDll] = module;

            return module;
        }

        internal void EnsureSymbolsLoaded(long baseOfDll)
        {
            if (worker.TryGetOperation(baseOfDll, out var operation))
                worker.ForceExecute(((SymbolDeferrableOperation) operation).SymbolOp);
        }

        internal bool AreSymbolsLoaded(long baseOfDll)
        {
            if (worker.TryGetOperation(baseOfDll, out var operation))
            {
                var op = ((SymbolDeferrableOperation) operation).SymbolOp;

                return op.IsCompleted;
            }

            if (loadedNativeModules.TryGetValue(baseOfDll, out var unmanagedModule))
            {
                return true;
            }

            return false;
        }

        internal void EnsureModuleLoaded(long baseOfDll, bool symbolsOnly)
        {
            if (loadedNativeModules.ContainsKey(baseOfDll))
                return;

            if (worker.TryGetOperation(baseOfDll, out var operation))
            {
                if (symbolsOnly)
                    worker.ForceExecute(((SymbolDeferrableOperation) operation).SymbolOp);
                else
                    worker.ForceExecute(operation);
            }
        }

        private void EnsureModuleLoaded(string mask)
        {
            if (mask == null)
                return;

            var bang = mask.IndexOf('!');

            if (bang != -1 && bang > 0)
            {
                var moduleName = mask.Substring(0, bang);

                if (moduleName.Contains("*"))
                    throw new NotImplementedException("Ensuring modules are loaded for a module containing a mask (implying we may need to force load all modules?) is not implemented");
                else
                {
                    foreach (var module in loadedNativeModules)
                    {
                        if (module.Value.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }

                    //It's not a loaded module; try pending
                    if (worker.TryGetOperation(moduleName, out var operation))
                    {
                        worker.ForceExecute(((SymbolDeferrableOperation) operation).SymbolOp);
                    }
                }
            }
        }

        internal bool TryGetModuleBase(long address, out long moduleBase)
        {
            foreach (var item in loadedModuleRanges)
            {
                if (address >= item.Key && address <= item.Value)
                {
                    moduleBase = item.Key;
                    return true;
                }
            }

            moduleBase = default;
            return false;
        }

        #endregion

        public void SetNativeGlobalValue<T>(string name, T value)
        {
            if (!TryNativeSymFromName(name, out var symbolInfo))
                throw new NotImplementedException();

            if (value is bool b)
                extension.WriteVirtual(symbolInfo.Address, b ? (byte) 1 : (byte) 0);
            else
                throw new NotImplementedException();
        }

        public IntPtr GetFunctionTableEntry(long address)
        {
            if (TryGetModuleBase(address, out var moduleBase))
                EnsureModuleLoaded(moduleBase, true);

            return dbgHelp.SymFunctionTableAccess64(address);
        }

        private IUnmanagedSymbolModule CreateModule(string filePath, long baseOfDll, int dllSize)
        {
            /*if (dbgHelp.TrySymGetDiaSession(baseOfDll, out var session) == S_OK)
            {
                return new MicrosoftPdbSymbolModule(
                    session,
                    dbgHelp,
                    sourceFileProvider,
                    filePath,
                    baseOfDll,
                    dllSize
                );
            }*/

            return new DbgHelpSymbolModule(dbgHelp, filePath, baseOfDll, dllSize);
        }
    }
}
