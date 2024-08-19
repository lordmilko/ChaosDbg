using System;
using System.IO;
using ChaosDbg.Cordb;
using ChaosDbg.Debugger;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug;

namespace ChaosDbg.Symbol
{
    /// <summary>
    /// Provides facilities for resolving symbols for the modules within a process.
    /// </summary>
    public partial class DebuggerSymbolProvider : INativeStackWalkerFunctionTableProvider, IDisposable
    {
        public IntPtr hProcess => dbgHelp.hProcess;

        private IDebuggerSymbolProviderExtension extension;
        private MicrosoftPdbSourceFileProvider sourceFileProvider;

        internal unsafe DebuggerSymbolProvider(
            IntPtr hProcess,
            int id,
            IDebuggerSymbolProviderExtension extension,
            IDbgHelpProvider dbgHelpProvider,
            MicrosoftPdbSourceFileProvider sourceFileProvider)
        {
            //dbghelp!ReadInProcMemory won't attempt to call ReadProcessMemory if you have a callback
            //specified. As such, we need to plug in a read handler ourselves
            dbgHelp = dbgHelpProvider.Acquire(hProcess);

            if (dbgHelp is LegacyDbgHelp l)
            {
                l.GetProcesses = () =>
                {
                    var globals = NativeReflector.GetGlobal<WellKnownSymbol.DbgHelp.GLOBALS>("dbghelp!g");

                    return Tuple.Create<object, int>(globals, (int) globals.NumProcesses);
                };
            }

            using var dbgHelpHolder = new DisposeHolder(dbgHelp);

            //Eagerly loading all symbols on a background thread sounds great and all, but the reality is that attempts to do anything on the UI thread will
            //hang because a million things are all blocking trying to enter the DbgHelp lock. So what we really need is a priority queue: module loads add
            //themselves to the queue, but if the UI wants to do something it jumps straight to the front

            dbgHelp.Callback.OnReadMemory = data =>
            {
                return extension.TryReadVirtual(
                    data->addr,
                    data->buf,
                    data->bytes,
                    out *data->bytesread
                ) == HRESULT.S_OK;
            };

            this.extension = extension;
            this.sourceFileProvider = sourceFileProvider;

            //Resolving symbols can be very slow, so we defer this to a background thread
            worker = new DispatcherPriorityQueueWorker($"Symbol Resolution Thread {id}");

            workerContext = new SymbolDeferrableOperationContext(
                dbgHelp,
                worker,
                this
            );

            nativeSymbolProvider = new NativeSymbolProvider(hProcess, dbgHelpProvider, sourceFileProvider);

            dbgHelpHolder.SuppressDispose();
        }

        public bool TrySymFromAddr(long address, SymFromAddrOption options, out IDisplacedSymbol result)
        {
            SetImpliedOptions(ref options);

            //We must try managed symbols first in case this address is for a JIT Helper.
            //This will also return any symbols (safe or dangerous) that have been previously
            //cached

            result = default;

            if (options.HasFlag(SymFromAddrOption.Managed))
            {
                if (TryManagedSymFromAddr(address, out result))
                    return true;
            }

            if (options.HasFlag(SymFromAddrOption.CLR))
            {
                //Is it a CLR internal helper method? This method will attempt to force load the CLR with DbgHelp if it isn't registered already,
                //however if we're planning on querying native symbols anyway, we'll bail out in order to do that below. As such, this must come
                //before the handling of SymFromAddrOption.Native
                if (TryCLRSymFromAddr(address, options, out result))
                    return true;
            }

            if (options.HasFlag(SymFromAddrOption.Native))
            {
                if (TryNativeSymFromAddr(address, out result))
                    return true;
            }

            if (options.HasFlag(SymFromAddrOption.Fallback))
            {
                //We already force loaded the module (if it exists, and was queued) in TryNativeSymFromAddr(). We couldn't find a symbol, so just check if
                //we at least have a module that we can return some information for

                if (TryGetNativeModuleBase(address, out var moduleBase))
                {
                    var name = Path.GetFileNameWithoutExtension(Kernel32.GetModuleFileNameExW(hProcess, (IntPtr) moduleBase));
                    var displacement = address - moduleBase;

                    result = new DisplacedMissingSymbol(displacement, name, address);
                    return true;
                }
            }

            if (options.HasFlag(SymFromAddrOption.DangerousManaged))
            {
                //As a last resort try queries that may result in mscordacwks throwing an exception as a result plowing a head trying to treat
                //a memory address as something its not
                if (TryDangerousManagedSymFromAddr(address, options))
                    return true;
            }

            return false;
        }

        private void SetImpliedOptions(ref SymFromAddrOption options)
        {
            //Set these in reverse order of precedence

            if (options.HasFlag(SymFromAddrOption.Thunk))
                options |= SymFromAddrOption.Managed;

            if (options.HasFlag(SymFromAddrOption.Fallback))
                options |= SymFromAddrOption.Native;

            if (options.HasFlag(SymFromAddrOption.Managed))
                options |= SymFromAddrOption.CLR;
        }

        public void Dispose()
        {
            eagerCLRSymbolsThread?.Join();

            nativeSymbolProvider?.Dispose();
            worker?.Dispose();
            dbgHelp?.Dispose();
        }
    }
}
