using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ChaosDbg.Debugger;
using ChaosLib;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Symbol
{
    class SymbolDeferrableSubOperation : AsyncDeferrableSubOperation
    {
        public DispatcherOperation SlowOperation { get; private set; }

        public SymbolDeferrableSubOperation(int priority, CancellationToken cancellationToken) : base(priority, cancellationToken)
        {
        }

        protected override void DoExecute(bool forceSynchronous)
        {
            if (Priority == 0 && forceSynchronous)
            {
                var parent = (SymbolDeferrableOperation) ParentOperation;

                parent.SymbolModule = parent.Context.SymbolProvider.AddNativeModule(
                    parent.Name,
                    parent.BaseOfDll,
                    parent.PEFile.OptionalHeader.SizeOfImage
                );

                Debug.Assert(parent.SymbolModule != null);

                Task = Task.CompletedTask;
                Status = DeferrableOperationStatus.Completed;
            }
            else
                base.DoExecute(forceSynchronous);
        }

        protected override async Task<bool> DoExecuteAsync(bool forceSynchronous)
        {
            using var logContextHolder = new LogContextHolder(CancellationToken);

            var parent = (SymbolDeferrableOperation) ParentOperation;

            var context = parent.Context;

            //Check whether someone's already force loaded this module. Note that we don't call GetPdbAsync under the lock,
            //because the whole point is we DO want to enable querying multiple PDBs concurrently. It doesn't matter if we race
            //with GetPdbAsync and somebody force loading the module, because we'll then lock again below when we actually go to add it
            //We've already loaded this module, nothing to do
            if (context.SymbolProvider.TryGetSymbolModule(parent.BaseOfDll, out var existingModule))
            {
                parent.SymbolModule = existingModule;
                return true;
            }

            //Before looking in the symstore, check if we have a local PDB
            string foundPdb;
            var expectedLocalPdb = Path.ChangeExtension(parent.Name, ".pdb");

            if (File.Exists(expectedLocalPdb))
                foundPdb = expectedLocalPdb;
            else
            {
                //Because DbgHelp is single threaded, we try and locate the symbols ourselves first. This is super useful for managed assemblies where there
                //might be a PDB but no EXE available.
                foundPdb = await context.SymbolClient.GetPdbAsync(parent.Name, true, CancellationToken);
            }

            logContextHolder.Refresh();

            if (Status == DeferrableOperationStatus.Aborted)
            {
                //The PEOp depends on having a SymbolModule set; so if we're not going to run, it can't either
                parent.PEOp.Abort();
                return true;
            }

            if (foundPdb != null)
            {
                //If we've already loaded this module, there's nothing to do
                if (context.SymbolProvider.TryGetSymbolModule(parent.BaseOfDll, out existingModule))
                {
                    parent.SymbolModule = existingModule;
                    return true;
                }

                var dllSize = parent.PEFile.OptionalHeader.SizeOfImage;

                //Specifying SLMFLAG_NO_SYMBOLS to SymLoadModuleEx means there'll be absolutely no symbols, now and forever, so we might not want to do that

                //Load the module directly using the PDB

                HRESULT hr;

                if ((hr = context.DbgHelp.TrySymLoadModuleEx(out _, imageName: foundPdb, baseOfDll: parent.BaseOfDll, dllSize: dllSize)) != S_OK)
                {
                    if (Status == DeferrableOperationStatus.Aborted)
                    {
                        //The PEOp depends on having a SymbolModule set; so if we're not going to run, it can't either
                        parent.PEOp.Abort();
                        return true;
                    }

                    Log.Debug<SymbolDeferrableSubOperation>("Failed to load symbols for found PDB {pdb} ({hr}). Scheduling retry at low priority", foundPdb, hr);

                    //Sometimes we can get a file not found when attempting to load the PDB,
                    //despite the fact a PDB clearly does exist. Since I've only observed this with managed PDBs,
                    //I would say DbgHelp likely doesn't support portable PDBs. Fallback to trying to load the image by itself
                    LoadNativeSymbolsLowPriorityAsync();

                    return false;
                }
                else
                {
                    parent.SymbolModule = context.SymbolProvider.RegisterFullSymbolModule(parent.Name, parent.BaseOfDll, dllSize);
                    return true;
                }
            }
            else
            {
                //PDB doesn't appear to have symbols. We need to ensure that we've at least registered the module with DbgHelp
                LoadNativeSymbolsLowPriorityAsync();

                return false;
            }
        }

        private void LoadNativeSymbolsLowPriorityAsync()
        {
            var parent = (SymbolDeferrableOperation) ParentOperation;

            var context = parent.Context;

            ProcessNextOperation = false;

            var slowOp = context.Worker.DispatcherThread.InvokeAsync(() =>
            {
                //We failed to find symbols the easy way. If this is a managed executable, assume there won't be any symbols.
                //It's not inconceivable that it's a C++/CLI module or there's exports or something though

                var isCLR = parent.PEFile.OptionalHeader.CorHeaderTableDirectory.RelativeVirtualAddress != 0;

                if (Status == DeferrableOperationStatus.Aborted)
                {
                    //The PEOp depends on having a SymbolModule set; so if we're not going to run, it can't either
                    parent.PEOp.Abort();
                    return;
                }

                parent.SymbolModule = context.SymbolProvider.AddNativeModule(parent.Name, parent.BaseOfDll, parent.PEFile.OptionalHeader.SizeOfImage, isCLR ? ModuleSymFlag.SLMFLAG_NO_SYMBOLS : 0);

                //Read all flags excluding the Cor20Header (which we already read)
                //ReadExtraDataDirectories(baseOfDll, peFile, symbolModule, resolvePESymbols, PEFileDirectoryFlags.All & ~PEFileDirectoryFlags.Cor20Header);
                //throw new NotImplementedException();

                NextOperation?.Execute(false);
            }, priority: 2);

            SlowOperation = slowOp;
        }

        protected override void WaitIfAsync()
        {
            //During LoadNativeSymbolsLowPriorityAsync we may decide we need to defer the task as a slow operation.
            //In that case, SlowOperation and SlowTask will already be set by the time this returns
            base.WaitIfAsync();

            if (SlowOperation != null)
                SlowOperation.Wait();
        }
    }
}
