using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChaosDbg.Analysis;
using ChaosDbg.Debugger;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ClrDebug;

namespace ChaosDbg.Symbol
{
    class PEFileDeferrableSubOperation : DeferrableSubOperation
    {
        public bool ResolvePESymbols { get; }

        public PEFileDeferrableSubOperation(bool resolvePeSymbols) : base(1)
        {
            ResolvePESymbols = resolvePeSymbols;
        }

        protected override void DoExecute(bool forceSynchronous)
        {
            var parent = (SymbolDeferrableOperation) ParentOperation;

            if (parent.SymbolModule == null)
            {
                var symbolOpTask = parent.SymbolOp?.Task;

                if (symbolOpTask != null && symbolOpTask.IsCompleted)
                {
                    if (parent.SymbolModule == null)
                        Debug.Assert(false, "Expected to have a SymbolModule"); //the symbol module wasnt set
                }
                else
                {
                    //The symbol operation hasn't completed yet. The PEOp was registered as the next operation
                    Status = DeferrableOperationStatus.Pending;
                    return;
                }
            }

            if (parent.SymbolModule == null)
                throw new NotImplementedException();

            ReadExtraDataDirectories(parent.SymbolModule, PEFileDirectoryFlags.All);
        }

        private void ReadExtraDataDirectories(ISymbolModule symbolModule, PEFileDirectoryFlags flags)
        {
            var parent = (SymbolDeferrableOperation) ParentOperation;

            var context = parent.Context;

            //All of the hopping around reading various bits of memory for the PEFile data directories is kind of slow, while reading the whole thing at once is quite fast. However, sometimes
            //we can fail to read the entire image. In that scenario, fallback to reading directly from memory
            Stream dataDirBuffer = new MemoryStream(parent.PEFile.OptionalHeader.SizeOfImage);

            lock (context.ProcessMemoryStreamLock)
            {
                context.ProcessMemoryStream.Seek(parent.BaseOfDll, SeekOrigin.Begin);
                context.ProcessMemoryStream.CopyTo(dataDirBuffer);
            }

            var needLock = false;

            //Fortunately this only rarely happens
            if (dataDirBuffer.Length != parent.PEFile.OptionalHeader.SizeOfImage)
            {
                //Check if the module has unloaded before we plow ahead trying to read it out of the target process. Strictly speaking the correct thing to do here would be
                //to check the InMemoryOrderModuleList in the PEB, but I mean if we can't even read the beginning of the module, we're going to crash anyway so may as well
                //bail out now
                if (Kernel32.TryReadProcessMemory(context.SymbolProvider.hProcess, (IntPtr) parent.BaseOfDll, IntPtr.Size, out _) == HRESULT.ERROR_PARTIAL_COPY)
                {
                    Status = DeferrableOperationStatus.Failed;
                    RaiseOnComplete();
                    return;
                }

                dataDirBuffer = new RelativeToAbsoluteStream(context.ProcessMemoryStream, parent.BaseOfDll);
                needLock = true;
            }

            //We have an unfortunate timing issue here. We want to read the whole PE File, but doing so
            //can require access to symbols in order to read ExceptionData correctly. So, we must populate
            //the data directories after we've read the symbols

            PESymbolResolver symbolResolver = null;

            if (ResolvePESymbols)
            {
                //We don't need to worry about getting symbol information for managed modules. If a module is a C++/CLI module,
                //we'll get a native module load event prior to this managed load event, and we'll get the native symbol information
                //in that event.
                symbolResolver = new PESymbolResolver(symbolModule);
            }

            try
            {
                if (needLock)
                    Monitor.Enter(context.ProcessMemoryStreamLock);

                parent.PEFile.ReadDataDirectories(dataDirBuffer, flags, symbolResolver);
            }
            finally
            {
                if (needLock)
                    Monitor.Exit(context.ProcessMemoryStreamLock);
            }
        }
    }
}
