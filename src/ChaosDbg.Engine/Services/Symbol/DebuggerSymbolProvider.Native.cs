using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ChaosDbg.Analysis;
using ChaosDbg.Debugger;
using ChaosDbg.SymStore;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Symbol
{
    //Members that pertain to resolving unmanaged symbols

    class PendingOp
    {
        public DispatcherOperation Op { get; }

        public string Name { get; }

        public ManualResetEventSlim HasSymbols { get; }

        public long Size { get; }

        public bool IsCancelled { get; set; }

        public CancellationToken CancellationToken => cts.Token;

        private CancellationTokenSource cts = new CancellationTokenSource();

        public PendingOp(DispatcherOperation op, string name, long size, bool hasSymbolsAlready)
        {
            Op = op;
            Name = name;
            Size = size;

            //If we eagerly loaded symbols, set the event to signalled immediately
            HasSymbols = new ManualResetEventSlim(hasSymbolsAlready);
        }

        public void Dispose()
        {
            //Set the wait in case anyone is waiting on it
            HasSymbols.Set();
            HasSymbols.Dispose();
            IsCancelled = true;

            //Throwing an exception (even on a background thread) seems to slow down the load performance
            //cts.Cancel();
        }
    }

    public partial class DebuggerSymbolProvider
    {
        /// <summary>
        /// Provides facilities for offloading symbol resolution to a background thread.
        /// </summary>
        private DispatcherThread symbolThread;

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

        /// <summary>
        /// Stores all operations that are currently pending on the SymbolThread
        /// </summary>
        private Dictionary<long, PendingOp> pendingNativeOperations = new();

        /// <summary>
        /// A lock that controls access to the pendingOperations dictionary
        /// </summary>
        private object pendingNativeOperationsLock = new object();

        private DispatcherPriorityQueueWorker worker;
        private SymbolDeferrableOperationContext workerContext;

        private object frameContextLock = new object();

        /// <summary>
        /// The underlying DbgHelp session that is used to provide access to native symbols
        /// </summary>
        private IDbgHelp dbgHelp;

        private NativeSymbolProvider nativeSymbolProvider;

        /// <summary>
        /// Used by the SymbolThread to asynchronously download multiple symbols in parallel to pre-download any symbols,
        /// tell DbgHelp where the target PDB is, or de-prioritize a given module's symbols until higher priority modules
        /// have been processed
        /// </summary>
        private SymbolClient symbolClient = new SymbolClient(new NullSymStoreLogger());

        /// <summary>
        /// Gets or sets the callback that is used for unwinding call frames pertaining to dynamically generated code in x64 processes.
        /// </summary>
        public PSYMBOL_FUNCENTRY_CALLBACK64 FunctionEntryCallback
        {
            get => dbgHelp.FunctionEntryCallback;
            set => dbgHelp.FunctionEntryCallback = value;
        }

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
            if (TryGetPendingModule(address, out moduleBase))
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
                    if (!hasLooped && TryGetPendingModule(address, out var moduleBase))
                    {
                        EnsureModuleLoaded(moduleBase, true);

                        hasLooped = true;
                        goto start;
                    }
                }
            }
            else
            {
                if (!hasLooped && TryGetPendingModule(address, out var moduleBase))
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

        public unsafe IUnmanagedSymbol[] NativeSymEnumSymbols(string mask, Func<SymbolInfo, bool> predicate = null)
        {
            var results = new List<IUnmanagedSymbol>();

            EnsureModuleLoaded(mask);

            dbgHelp.SymEnumSymbolsEx(
                0,
                (pSymInfo, symSize, userContext) =>
                {
                    var info = new SymbolInfo((SYMBOL_INFO*) pSymInfo);

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
            if (TryGetPendingModule(ip, out var moduleBase))
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
            ((LegacyDbgHelp) dbgHelp).AddVirtualModule(imageName, baseOfDll, dllSize, flags, HookLock);

            var localOptions = dbgHelp.GlobalOptions;

            //Debug.Assert(!localOptions.HasFlag(ClrDebug.DbgEng.SYMOPT.DEFERRED_LOADS), "Attempted to add a module while deferred loads is set. This is not allowed");
            var module = CreateModule(imageName, baseOfDll, dllSize);

            TryRegisterCLR(module);

            loadedNativeModules[baseOfDll] = module;

            //If we're adding via low priority, we don't want to abort the operation before it's been able to mark it as completed or it might get upset that the task source already has a result
            ClearPendingOperation(baseOfDll, false);

            return module;
        }

        #endregion
        #region Remove

        public void RemoveNativeModule(long baseOfDll)
        {
            var op = ClearPendingOperation(baseOfDll, true);

            void RemoveModuleInternal()
            {
                if (loadedNativeModules.TryGetValue(baseOfDll, out _))
                {
                    //Apparently, as long as the key is still present, TryRemove will always succeed
                    loadedNativeModules.TryRemove(baseOfDll, out _);

                    dbgHelp.SymUnloadModule64(baseOfDll);
                }
            }

            //If we've loaded the module already, we need to unload it. If we're merely queued loading the module however, we need to remove it from the queue

            if (op == null)
            {
                //The task already finished executing
                RemoveModuleInternal();
            }
            else if (op.Status == DispatcherOperationStatus.Aborted)
            {
                //The task was queued and we successfully cancelled it

                //Just in case we force loaded it
                RemoveModuleInternal();
            }
            else
            {
                //The task is in the process of executing

                //Just in case it completed by the time we entered the lock
                RemoveModuleInternal();

                op.Completed += (s, e) =>
                {
                    RemoveModuleInternal();
                };
            }

            if (op != null && !op.Task.IsCanceled)
            {
                //When we defer something to the slow queue, I think the primary deferred task might return
                //another task, but we don't wait for that to return, and we also don't cancel its CTS either
                //as an exception thrown on the background. I don't know whether this is going to cause issues or not

                op.Task.Wait();
            }
        }

        #endregion
        #region Defer

        public void DeferCalculateModuleInfoAsync(string name, long baseOfDll, IPEFile peFile, bool resolvePESymbols)
        {
            //Don't allow the module to add itself before we've registered its operation

            //We can't rely on the image base and size in the PE File, it can be wrong in managed executables when loaded into memory

            if (Path.GetFileNameWithoutExtension(name).ToLower() == "ntdll")
            {
                //We're going to hit ntdll immediately when we hit the loader breakpoint, and messing around with all this synchronization seems to cause a delay, so just load it ASAP
                var symbolModule = AddNativeModule(name, baseOfDll, peFile.OptionalHeader.SizeOfImage);

                //Just need to load the PE data now
                lock (pendingNativeOperationsLock)
                {
                    var op = symbolThread.InvokeAsync(
                        () =>
                        {
                            ReadExtraDataDirectories(baseOfDll, peFile, symbolModule, true, PEFileDirectoryFlags.All);

                            ClearPendingOperation(baseOfDll, false);
                        },
                        priority: 1
                    );

                    Log.Debug<DebuggerSymbolProvider>("Modifying pendingNativeOperations (DeferCalculateModuleInfoAsync {dll})", name);
                    pendingNativeOperations.Add(baseOfDll, new PendingOp(op, name, peFile.OptionalHeader.SizeOfImage, true));
                }
            }
            else
            {
                lock (pendingNativeOperationsLock)
                {
                    var op = symbolThread.InvokeAsync(
                        () => LoadNativeSymbolsAsync(name, baseOfDll, peFile, resolvePESymbols),
                        priority: 1
                    );

                    Log.Debug<DebuggerSymbolProvider>("Modifying pendingNativeOperations (DeferCalculateModuleInfoAsync {dll})", name);
                    pendingNativeOperations.Add(baseOfDll, new PendingOp(op, name, peFile.OptionalHeader.SizeOfImage, false));
                }
            }
        }

        private async Task<DispatcherOperation> LoadNativeSymbolsAsync(string filePath, long baseOfDll, IPEFile peFile, bool resolvePESymbols)
        {
            //Check whether someone's already force loaded this module. Note that we don't call GetPdbAsync under the lock,
            //because the whole point is we DO want to enable querying multiple PDBs concurrently. It doesn't matter if we race
            //with GetPdbAsync and somebody force loading the module, because we'll then lock again below when we actually go to add it
            //We've already loaded this module, nothing to do
            if (loadedNativeModules.ContainsKey(baseOfDll))
                return null;

            PendingOp pendingOp;

            lock (pendingNativeOperationsLock)
                pendingNativeOperations.TryGetValue(baseOfDll, out pendingOp);

            //Before looking in the symstore, check if we have a local PDB
            string result;
            var localPdb = Path.ChangeExtension(filePath, ".pdb");

            if (File.Exists(localPdb))
                result = localPdb;
            else
            {
                //Because DbgHelp is single threaded, we try and locate the symbols ourselves first. This is super useful for managed assemblies where there
                //might be a PDB but no EXE available.
                result = await symbolClient.GetPdbAsync(filePath, pendingOp?.CancellationToken ?? default);
            }

            if (pendingOp?.IsCancelled == true)
                return null;

            if (result != null)
            {
                //We've already loaded this module, nothing to do
                if (loadedNativeModules.ContainsKey(baseOfDll))
                    return null;

                var dllSize = peFile.OptionalHeader.SizeOfImage;

                //Specifying SLMFLAG_NO_SYMBOLS means there'll be absolutely no symbols, now and forever

                //Load the module directly using the PDB
                if (dbgHelp.TryAddVirtualModule(result, baseOfDll, dllSize, hookLock: HookLock) != S_OK)
                {
                    if (pendingOp?.IsCancelled == true)
                        return null;

                    //Sometimes we can get a file not found when attempting to load the PDB,
                    //despite the fact a PDB clearly does exist. Since I've only observed this with managed PDBs,
                    //I would say DbgHelp likely doesn't support portable PDBs. Fallback to trying to load the image by itself
                    return LoadNativeSymbolsLowPriorityAsync(filePath, baseOfDll, peFile, resolvePESymbols, pendingOp);
                }
                else
                {
                    var module = RegisterFullSymbolModule(filePath, baseOfDll, dllSize);

                    lock (pendingNativeOperationsLock)
                    {
                        if (pendingNativeOperations.TryGetValue(baseOfDll, out var opInfo))
                            opInfo.HasSymbols.Set();
                    }

                    ReadExtraDataDirectories(baseOfDll, peFile, module, resolvePESymbols, PEFileDirectoryFlags.All);

                    return ClearPendingOperation(baseOfDll, false);
                }
            }
            else
            {
                //PDB doesn't appear to have symbols. We need to ensure that we've at least registered the module with DbgHelp
                return LoadNativeSymbolsLowPriorityAsync(filePath, baseOfDll, peFile, resolvePESymbols, pendingOp);
            }
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

        void HookLock(object sender, DbgHelpSymbolCallback.XmlEvent e)
        {
            if (e is DbgHelpSymbolCallback.XmlActivityStartEvent)
            {
                Log.Debug<IDbgHelp>($"HookLock: calling {nameof(LegacyDbgHelp.ExitDbgHelp)} ({{message}})", e.ToString());
                ((LegacyDbgHelp) dbgHelp).ExitDbgHelp();
            }

            if (e is DbgHelpSymbolCallback.XmlActivityEndEvent)
            {
                Log.Debug<IDbgHelp>($"HookLock: calling {nameof(LegacyDbgHelp.EnterDbgHelp)} ({{message}})", e.ToString());
                ((LegacyDbgHelp) dbgHelp).EnterDbgHelp();
            }
        }

        private DispatcherOperation LoadNativeSymbolsLowPriorityAsync(
            string filePath,
            long baseOfDll,
            PEFile peFile,
            bool resolvePESymbols,
            PendingOp pendingOp)
        {
            return symbolThread.InvokeAsync(() =>
            {
                //We failed to find symbols the easy way. If this is a managed executable, assume there won't be any symbols.
                //It's not inconceivable that it's a C++/CLI module or there's exports or something though

                var isCLR = peFile.OptionalHeader.CorHeaderTableDirectory.RelativeVirtualAddress != 0;

                if (pendingOp?.IsCancelled == true)
                    return;

                var symbolModule = AddNativeModule(filePath, baseOfDll, peFile.OptionalHeader.SizeOfImage, isCLR ? ModuleSymFlag.SLMFLAG_NO_SYMBOLS : 0);

                if (pendingOp?.IsCancelled == true)
                    return;

                //Can't use our existing pendingOp object, as it may have been disposed
                lock (pendingNativeOperationsLock)
                {
                    if (pendingNativeOperations.TryGetValue(baseOfDll, out var opInfo))
                        opInfo.HasSymbols.Set();
                }

                if (pendingOp?.IsCancelled == true)
                    return;

                //Read all flags excluding the Cor20Header (which we already read)
                ReadExtraDataDirectories(baseOfDll, peFile, symbolModule, resolvePESymbols, PEFileDirectoryFlags.All & ~PEFileDirectoryFlags.Cor20Header);
            }, priority: 2);
        }

        private DispatcherOperation ClearPendingOperation(long baseOfDll, bool abort)
        {
            lock (pendingNativeOperationsLock)
            {
                if (pendingNativeOperations.TryGetValue(baseOfDll, out var opInfo))
                {
                    if (abort)
                        opInfo.Op.Abort();

                    opInfo.Dispose();

                    //We want to remove the item from the queue, but not abort the task source inside the operation itself, so that it can still be marked as completed
                    symbolThread.Dispatcher.Abort(opInfo.Op);

                    Log.Debug<DebuggerSymbolProvider>("Modifying pendingNativeOperations (ClearPendingOperation {dll})", opInfo.Name);
                    pendingNativeOperations.Remove(baseOfDll);

                    return opInfo.Op;
                }

                return null;
            }
        }

        internal void EnsureModuleLoaded(long baseOfDll, bool symbolsOnly)
        {
            PendingOp opInfo;

            lock (pendingNativeOperationsLock)
                pendingNativeOperations.TryGetValue(baseOfDll, out opInfo);

            //If the operation hasn't already completed, invoke it immediately. After resolving the symbols, the PEFile will be loaded
            if (opInfo?.Op != null)
            {
                symbolThread.Dispatcher.Abort(opInfo.Op);

                //The task may already be in the process of executing. Technically speaking as soon as an operation starts executing it should completely immediately
                //since it doesn't actually await LoadNativeSymbolsAsync
                if (opInfo.Op.Status == DispatcherOperationStatus.Pending || opInfo.Op.Status == DispatcherOperationStatus.Aborted)
                {
                    opInfo.Op.Invoke();
                }

                opInfo.Op.Wait();

                if (symbolsOnly)
                {
                    if (!opInfo.HasSymbols.IsSet)
                        opInfo.HasSymbols.Wait();
                }
                else
                {
                    //Need to wait for the PE data as well

                    var result = (Task) opInfo.Op.Result;

                    result.Wait();
                }
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
                    //Try and find a pending matching module
                    long match = 0;

                    lock (pendingNativeOperationsLock)
                    {
                        foreach (var pendingOp in pendingNativeOperations)
                        {
                            var opFileName = Path.GetFileNameWithoutExtension(pendingOp.Value.Name);

                            if (StringComparer.OrdinalIgnoreCase.Equals(moduleName, opFileName))
                            {
                                match = pendingOp.Key;
                                break;
                            }
                        }
                    }

                    if (match != 0)
                        EnsureModuleLoaded(match, true);
                }
            }
        }

        internal bool TryGetPendingModule(long address, out long moduleBase)
        {
            lock (pendingNativeOperationsLock)
            {
                foreach (var item in pendingNativeOperations)
                {
                    if (address >= item.Key && address <= item.Key + item.Value.Size)
                    {
                        moduleBase = item.Key;
                        return true;
                    }
                }
            }

            moduleBase = default;
            return false;
        }

        private void ReadExtraDataDirectories(long baseOfDll, PEFile peFile, ISymbolModule symbolModule, bool resolvePESymbols, PEFileDirectoryFlags flags)
        {
            //All of the hopping around reading various bits of memory for the PEFile data directories is kind of slow, while reading the whole thing at once is quite fast. However, sometimes
            //we can fail to read the entire image. In that scenario, fallback to reading directly from memory
            Stream dataDirBuffer = new MemoryStream(peFile.OptionalHeader.SizeOfImage);

            lock (workerContext.ProcessMemoryStreamLock)
            {
                workerContext.ProcessMemoryStream.Seek(baseOfDll, SeekOrigin.Begin);
                workerContext.ProcessMemoryStream.CopyTo(dataDirBuffer);
            }

            var needLock = false;

            //Fortunately this only rarely happens
            if (dataDirBuffer.Length != peFile.OptionalHeader.SizeOfImage)
            {
                dataDirBuffer = new RelativeToAbsoluteStream(workerContext.ProcessMemoryStream, baseOfDll);
                needLock = true;
            }

            //We have an unfortunate timing issue here. We want to read the whole PE File, but doing so
            //can require access to symbols in order to read ExceptionData correctly. So, we must populate
            //the data directories after we've read the symbols

            PESymbolResolver symbolResolver = null;

            if (resolvePESymbols)
            {
                //We don't need to worry about getting symbol information for managed modules. If a module is a C++/CLI module,
                //we'll get a native module load event prior to this managed load event, and we'll get the native symbol information
                //in that event.
                symbolResolver = new PESymbolResolver(symbolModule);
            }

            try
            {
                if (needLock)
                    Monitor.Enter(workerContext.ProcessMemoryStreamLock);

                peFile.ReadDataDirectories(dataDirBuffer, flags, symbolResolver);
            }
            finally
            {
                if (needLock)
                    Monitor.Exit(workerContext.ProcessMemoryStreamLock);
            }
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
            if (TryGetPendingModule(address, out var moduleBase))
                EnsureModuleLoaded(moduleBase, true);

            return DbgHelp.Native.SymFunctionTableAccess64(hProcess, address);
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
