using System;
using System.IO;
using ChaosDbg.Cordb;
using ChaosDbg.Debugger;
using ChaosLib;
using ChaosLib.Metadata;
using ClrDebug.DbgEng;

namespace ChaosDbg.Symbol
{
    /// <summary>
    /// Provides facilities for resolving symbols for the modules within a process.
    /// </summary>
    public partial class DebuggerSymbolProvider : IDisposable
    {
        //A memory stream that can be used to read memory directly from the target process
        private ProcessMemoryStream processMemoryStream;

        //A lock that controls access to the processMemoryStream
        private object processMemoryStreamLock = new object();

        public IntPtr hProcess => dbgHelp.hProcess;

        private IDebuggerSymbolProviderExtension extension;

        internal DebuggerSymbolProvider(IntPtr hProcess, IDebuggerSymbolProviderExtension extension)
        {
            dbgHelp = new DbgHelpSession(hProcess);

            if (dbgHelp.GlobalOptions.HasFlag(SYMOPT.DEFERRED_LOADS))
                throw new InvalidOperationException($"Cannot use {nameof(DebuggerSymbolProvider)} when {SYMOPT.DEFERRED_LOADS} is active. {nameof(DebuggerSymbolProvider)} design is currently predicated on doing eager loads on a background thread.");

            this.extension = extension;

            //Resolving symbols can be very slow, so we defer this to a background thread
            symbolThread = new DispatcherThread("Symbol Resolution Thread", queue: new DispatcherPriorityQueue());
            symbolThread.Start();

            processMemoryStream = new ProcessMemoryStream(hProcess);
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
            symbolThread?.Dispose();
            dbgHelp?.Dispose();
        }
    }
}
