using ChaosDbg.Debugger;
using ChaosDbg.SymStore;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;

namespace ChaosDbg.Symbol
{
    class SymbolDeferrableOperationContext
    {
        /// <summary>
        /// Used by the SymbolThread to asynchronously download multiple symbols in parallel to pre-download any symbols,
        /// tell DbgHelp where the target PDB is, or de-prioritize a given module's symbols until higher priority modules
        /// have been processed
        /// </summary>
        public SymbolClient SymbolClient { get; } = new SymbolClient(new NullSymStoreLogger());

        public IDbgHelp DbgHelp { get; }

        public DispatcherPriorityQueueWorker Worker { get; }

        public DebuggerSymbolProvider SymbolProvider { get; }

        //A memory stream that can be used to read memory directly from the target process
        public ProcessMemoryStream ProcessMemoryStream { get; }

        //A lock that controls access to the processMemoryStream
        public object ProcessMemoryStreamLock { get; } = new object();

        public SymbolDeferrableOperationContext(IDbgHelp dbgHelp, DispatcherPriorityQueueWorker worker, DebuggerSymbolProvider symbolProvider)
        {
            DbgHelp = dbgHelp;
            Worker = worker;
            SymbolProvider = symbolProvider;
            ProcessMemoryStream = new ProcessMemoryStream(symbolProvider.hProcess);
        }
    }

    class SymbolDeferrableOperation : DeferrableOperation
    {
        public SymbolDeferrableSubOperation SymbolOp { get; }
        public PEFileDeferrableSubOperation PEOp { get; }

        public ISymbolModule SymbolModule { get; set; }

        public string Name { get; }

        public long BaseOfDll => Key;

        public PEFile PEFile { get; }

        public SymbolDeferrableOperationContext Context { get; }

        public SymbolDeferrableOperation(
            string name,
            long baseOfDll,
            PEFile peFile,
            SymbolDeferrableOperationContext context,
            SymbolDeferrableSubOperation symbolOp,
            PEFileDeferrableSubOperation peOp) : base(baseOfDll, symbolOp, peOp)
        {
            SymbolOp = symbolOp;
            PEOp = peOp;

            symbolOp.NextOperation = peOp;

            Name = name;
            PEFile = peFile;
            Context = context;
        }
    }
}
