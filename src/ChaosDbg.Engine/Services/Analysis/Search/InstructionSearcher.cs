using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ChaosDbg.Disasm;
using ChaosDbg.Graph;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug;
using ClrDebug.DIA;
using Iced.Intel;

namespace ChaosDbg.Analysis
{
    class InstructionSearcher
    {
        public Dictionary<long, InstructionDiscoverySource> FunctionCandidates { get; } = new Dictionary<long, InstructionDiscoverySource>();

        public Dictionary<long, object> DataCandidates { get; } = new Dictionary<long, object>();
        public Dictionary<long, JumpTableMetadataRange> JumpTables { get; } = new Dictionary<long, JumpTableMetadataRange>();

        /// <summary>
        /// Gets the module that this searcher is trying to find metadata for.
        /// </summary>
        public PEMetadataModule Module { get; }

        /// <summary>
        /// Gets the options that were originally specified to this searcher.
        /// </summary>
        public PEMetadataSearchOptions Options { get; }

        /// <summary>
        /// Gets the disassembler that reads instructions from <see cref="memoryStream"/> inside this current process.
        /// </summary>
        public INativeDisassembler Disassembler { get; }

        public PEFile PEFile { get; }

        /// <summary>
        /// Gets a memory stream that contains a copy of the module, read from the remote process.
        /// </summary>
        private readonly MemoryStream memoryStream;

        /// <summary>
        /// A priority queue that stores the memory addresses that have yet to be investigated,
        /// sorted by the number of data sources that link to a given address, coupled with the
        /// strength of those data sources.
        /// </summary>
        private PriorityQueue<InstructionDiscoverySource, InstructionDiscoveryPriority> currentPriorityQueue;

        private PriorityQueue<InstructionDiscoverySource, InstructionDiscoveryPriority> trustedPriorityQueue;
        private PriorityQueue<InstructionDiscoverySource, InstructionDiscoveryPriority> untrustedPriorityQueue;

        internal Dictionary<long, INativeInstruction> discoveredInstructions = new Dictionary<long, INativeInstruction>();
        internal Dictionary<InstructionDiscoverySource, NativeCodeRegionCollection> discoveredCode = new();
        private DisasmFunctionResolutionContext ctx;
        private Dictionary<long, object> ignoreMap = new Dictionary<long, object>();
        private HashSet<long> knownCallTargets = new HashSet<long>();
        private HashSet<long> knownJumpTargets = new HashSet<long>();
        private Dictionary<long, HashSet<ImageScopeRecord>> exceptionUnwindMap = new Dictionary<long, HashSet<ImageScopeRecord>>();
        private List<(InstructionDiscoverySource, NativeCodeRegionCollection)> badFunctions = new();
        private List<InstructionDiscoverySource> deferredNewInstructions = new();
        internal HashSet<long> deferredSeen = new HashSet<long>();
        private ChunkVertexSplitter splitter = new ChunkVertexSplitter();

        internal long[] OrderedSymbolAddresses { get; private set; }

        public InstructionSearcher(
            PEMetadataSearchOptions options,
            PEMetadataModule module,
            PEFile peFile,
            Func<Stream, INativeDisassembler> createDisassembler)
        {
            Options = options;
            Module = module;
            this.PEFile = peFile;

            /* Read the entire module from the remote process. Not only should this provide faster disassembly
             * (no need to execute zillions of instructions in order to ReadProcessMemory()) but should also
             * provides faster and simpler byte pattern matching (juggling cached buffers from a stream in
             * order to support rewinding on a failed match, etc) */

            if (module.RemoteDisassembler.BaseStream is RemoteMemoryStream)
                module.RemoteDisassembler.BaseStream.Seek(module.Address, SeekOrigin.Begin);
            else
                module.RemoteDisassembler.BaseStream.Seek(0, SeekOrigin.Begin);

            memoryStream = new MemoryStream(peFile.OptionalHeader.SizeOfImage);
            module.RemoteDisassembler.BaseStream.CopyTo(memoryStream);

            var processStream = new AbsoluteToRelativeStream(memoryStream, module.Address);
            Disassembler = createDisassembler(processStream);

            //PriorityQueue sorts by lowest to highest (i.e. a priority of 1 is greater than a priority of 2). In our queue however,
            //higher values should be considered better than lower values.
            trustedPriorityQueue = CreatePriorityQueue();
            untrustedPriorityQueue = CreatePriorityQueue();

            ctx = new DisasmFunctionResolutionContext(this, processStream);
        }

        public void Search()
        {
            //Locate all locations in the module that we suspect may contain valid code
            CollectInstructionSources();

            //Locate all instructions in the module based on our instruction sources
            FindInstructions();

            //Iterate over all instructions we have and attempt to construct a network
            //of function chunks out of them
            var chunkBuilders = IdentifyFunctionChunks();

            /* Based on the relationships we identified between our function chunks,
             * we should now have a series of separate graphs, that most likely represent
             * functions. Sever the link between the chunks in an apparent function if
             * we have evidence that two chunks should not be in the same function (e.g.
             * the result of an unconditional jump has its own symbol) */

            CollectFunctions(chunkBuilders);
        }

        #region Prepare

        private void CollectInstructionSources()
        {
            /* We have a tricky ordering problem. We need to be able to do things like inform
             * when we have a noreturn function, or when we've gone past the known maximum length
             * of a given function chunk, but at the same time we need to be flexible enough that
             * we can do a good job of analyzing modules without symbols as well. And then, most
             * critically, we need to be able to _test_ with and without symbols as well. Rather
             * than just blast through all functions identified by each search technique one at
             * a time, we'll instead start by identifying function addresses that are known to _all_
             * of them. */

            //Trusted
            currentPriorityQueue = trustedPriorityQueue;

            //We must always record imports, so that if we see a call that leads to the IAT we don't attempt to disassemble it
            RecordImports();
            RecordConfig();

            if (Options.HasFlag(PEMetadataSearchOptions.Exports))
                HydrateExports();

            if (Options.HasFlag(PEMetadataSearchOptions.UnwindData))
                HydrateUnwindData();

            if (Options.HasFlag(PEMetadataSearchOptions.Symbols))
                HydrateSymbols();

            //Untrusted
            currentPriorityQueue = untrustedPriorityQueue;

            if (Options.HasFlag(PEMetadataSearchOptions.Patterns))
                HydratePatterns();

            /* Build a priority queue of search candidates, sorted by the "strength" of how
             * they've been found. e.g. a search candidate with UnwindData + Symbols is stronger
             * than a candidate with just UnwindData, which is stronger than a candidate
             * with Pattern + Export + Call + Symbol combined.
             *
             * If we locate a Call for a queue, we remove and re-add it to the queue in order
             * to update the priority. */

            foreach (var candidate in FunctionCandidates.Values)
            {
                if (candidate.TrustLevel == DiscoveryTrustLevel.Untrusted)
                    untrustedPriorityQueue.Enqueue(candidate, candidate.Priority);
                else
                    trustedPriorityQueue.Enqueue(candidate, candidate.Priority);
            }
        }

        private void RecordImports()
        {
            //Add in all import types. A function could be pointing to any one of these

            var iat = PEFile.ImportAddressTableDirectory;

            void EnsureImportWasntMatched(long addr)
            {
                //We could always just remove the address, but it imports shouldn't be getting matched as being functions in the first place
                if (FunctionCandidates.TryGetValue(addr, out _))
                    throw new InvalidOperationException($"Import at address {addr:X} should not have been added to the function candidate map");
            }

            if (iat != null)
            {
                foreach (var item in iat)
                {
                    var addr = item.StructOffset + Module.Address;

                    ignoreMap[addr] = item;

                    EnsureImportWasntMatched(addr);

                    if (DataCandidates.TryGetValue(addr, out var candidate) && candidate is InstructionDiscoverySource c)
                        c.FoundBy |= FoundBy.Import;
                }
            }
            else
            {
                //This directory can be missing in the header but imports may still exist

                if (PEFile.ImportDirectory != null)
                    throw new NotImplementedException("Don't know how to handle not having an IAT directory. It's still possible to have an IAT even without a dedicated directory. Should we try and pull the thunks from the normal import directory, or do something else entirely?");
            }

            var bound = PEFile.BoundImportTableDirectory;

            if (bound != null)
                throw new NotImplementedException("Don't know what members to look at to handle calls into bound imports");

            var delay = PEFile.DelayImportTableDirectory;

            if (delay != null)
            {
                foreach (var delayedModule in delay)
                {
                    if (delayedModule.ImportAddressTable != null)
                    {
                        foreach (var item in delayedModule.ImportAddressTable)
                        {
                            var addr = item.StructOffset + Module.Address;

                            ignoreMap[addr] = item;

                            EnsureImportWasntMatched(addr);

                            if (DataCandidates.TryGetValue(addr, out var candidate) && candidate is InstructionDiscoverySource c)
                                c.FoundBy |= FoundBy.Import;
                        }
                    }
                }
            }
        }

        private void RecordConfig()
        {
            var config = PEFile.LoadConfigDirectory;

            if (config != null)
            {
                var dataCandidates = new[]
                {
                    config.GuardCFCheckFunctionPointer,
                    config.GuardCFDispatchFunctionPointer,
                    config.GuardCFFunctionTable,
                    config.GuardEHContinuationTable,
                    config.GuardMemcpyFunctionPointer,
                    config.GuardXFGCheckFunctionPointer,
                    config.GuardXFGDispatchFunctionPointer,
                    config.GuardXFGTableDispatchFunctionPointer,
                    config.SecurityCookie
                };

                foreach (var item in dataCandidates)
                {
                    if (item != 0)
                    {
                        AddCandidate(DataCandidates, null, item, FoundBy.Config, DiscoveryTrustLevel.Trusted);
                        ignoreMap[item] = null;
                    }
                }

                var stream = Disassembler.BaseStream;
                var reader = new PEBinaryReader(stream);

                if (Options.HasFlag(PEMetadataSearchOptions.GuardCFFunctionTable))
                    HydrateGuardCFFunctionTable(config, reader);

                if (Options.HasFlag(PEMetadataSearchOptions.GuardEHContinuationTable))
                    HydrateGuardEHContinuationTable(config, reader);
            }
        }

        private void HydrateGuardCFFunctionTable(ImageLoadConfigDirectory config, PEBinaryReader reader)
        {
            //__guard_fids_table
            if (config.GuardCFFunctionTable != 0 && config.GuardCFFunctionCount != 0)
            {
                reader.Seek(config.GuardCFFunctionTable);
                var entries = new List<(int rva, IMAGE_GUARD_FLAG b)>();

                //https://learn.microsoft.com/en-us/windows/win32/secbp/pe-metadata

                //The GFIDS table is an array of 4 + n bytes, where n is given by ((GuardFlags & IMAGE_GUARD_CF_FUNCTION_TABLE_SIZE_MASK) >> IMAGE_GUARD_CF_FUNCTION_TABLE_SIZE_SHIFT)
                var stride = (int) (config.GuardFlags & IMAGE_GUARD.CF_FUNCTION_TABLE_SIZE_MASK);
                var shifted = stride >> ImageLoadConfigDirectory.CF_FUNCTION_TABLE_SIZE_SHIFT;

                var xfgTargets = new List<long>();

                for (var i = 0; i < config.GuardCFFunctionCount; i++)
                {
                    var rva = reader.ReadInt32();
                    IMAGE_GUARD_FLAG b = 0;

                    switch (shifted)
                    {
                        case 0:
                            break;

                        case 1:
                            b = (IMAGE_GUARD_FLAG) reader.ReadByte();
                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle a shifted value of {shifted}");
                    }

                    var searchData = AddFunctionCandidate(Module.Address + rva, FoundBy.Config, DiscoveryTrustLevel.Trusted, false);
                    searchData.FoundBySubType |= FoundBySubType.GuardCFFunctionTable;

                    if (b.HasFlag(IMAGE_GUARD_FLAG.FID_XFG))
                    {
                        //This function has 8 XFG bytes before it
                        xfgTargets.Add(Module.Address + rva - 8);
                    }

                    entries.Add((rva, b));
                }

                foreach (var target in xfgTargets)
                {
                    reader.Seek(target);
                    var bytes = reader.ReadBytes(8);

                    DataCandidates[target] = new XfgMetadataInfo(target, bytes);
                }
            }
        }

        private void HydrateGuardEHContinuationTable(ImageLoadConfigDirectory config, PEBinaryReader reader)
        {
            /* https://learn.microsoft.com/en-us/cpp/build/reference/guard-enable-eh-continuation-metadata?view=msvc-170
             * The instruction pointer of the __except block isn't expected to be on the shadow stack, because it would fail instruction pointer validation.
             * The /guard:ehcont compiler switch generates an "EH Continuation Table". It contains a sorted list of the RVAs of all valid exception handling
             * continuation targets in the binary */

            //__guard_eh_cont_table
            if (config.GuardEHContinuationTable != 0 && config.GuardEHContinuationCount != 0)
            {
                //no need to restore position, we havent started disassembling yey
                reader.Seek(config.GuardEHContinuationTable);

                var entries = new List<(int rva, byte b)>();

                for (var i = 0; i < config.GuardEHContinuationCount; i++)
                {
                    var rva = reader.ReadInt32();
                    var unknown = reader.ReadByte();

                    var searchData = AddFunctionCandidate(Module.Address + rva, FoundBy.Config, DiscoveryTrustLevel.Trusted, false);
                    searchData.FoundBySubType |= FoundBySubType.GuardEHContinuationTable;

                    entries.Add((rva, unknown));
                }
            }
        }

        private void HydrateExports()
        {
            if (PEFile.ExportDirectory != null)
            {
                foreach (var export in PEFile.ExportDirectory.Exports)
                {
                    if (export is ImageForwardedExportInfo)
                        throw new NotImplementedException("Don't know how to handle forwarded exports");

                    var normalExport = (ImageExportInfo) export;
                    var searchData = AddFunctionCandidate(normalExport.FunctionAddress, FoundBy.Export, DiscoveryTrustLevel.SemiTrusted, false);
                    searchData.Export = normalExport;
                }
            }
        }

        private void HydrateUnwindData()
        {
            if (PEFile.ExceptionDirectory != null)
            {
                foreach (var runtimeFunctionInfo in PEFile.ExceptionDirectory)
                {
                    var searchData = AddFunctionCandidate(runtimeFunctionInfo.BeginAddress + Module.Address, FoundBy.RuntimeFunction, DiscoveryTrustLevel.Trusted, false);

                    searchData.RuntimeFunction = runtimeFunctionInfo;

                    if (((int) runtimeFunctionInfo.UnwindData.Flags & (int) UNW_FLAG.FHANDLER) != 0)
                    {
                        var handler = runtimeFunctionInfo.UnwindData.ExceptionHandler;

                        if (handler != 0)
                            AddFunctionCandidate(handler + Module.Address, FoundBy.FHandler, DiscoveryTrustLevel.Trusted, false);
                    }

                    if (runtimeFunctionInfo.UnwindData.TryGetExceptionData(out var exceptionData) && exceptionData is ImageScopeTable table)
                    {
                        foreach (var record in table)
                        {
                            void GetOrAdd(long addr)
                            {
                                if (exceptionUnwindMap.TryGetValue(addr, out var set))
                                {
                                    set.Add(record);
                                }
                                else
                                {
                                    set = new HashSet<ImageScopeRecord>
                                    {
                                        record
                                    };

                                    exceptionUnwindMap[addr] = set;
                                }
                            }

                            var beginAddress = record.BeginAddress + Module.Address;
                            GetOrAdd(beginAddress);
                            searchData = AddFunctionCandidate(beginAddress, FoundBy.UnwindInfo, DiscoveryTrustLevel.Trusted, false);
                            searchData.FoundBySubType = FoundBySubType.ScopeRecordBegin;

                            var endAddress = record.EndAddress + Module.Address;
                            GetOrAdd(endAddress);
                            searchData = AddFunctionCandidate(endAddress, FoundBy.UnwindInfo, DiscoveryTrustLevel.Trusted, false);
                            searchData.FoundBySubType |= FoundBySubType.ScopeRecordEnd;

                            if (record.HandlerAddress > 1)
                            {
                                //Ignore EXCEPTION_EXECUTE_HANDLER (1),
                                //EXCEPTION_CONTINUE_SEARCH (0)
                                //EXCEPTION_CONTINUE_EXECUTION (-1)
                                var handlerAddress = record.HandlerAddress + Module.Address;
                                GetOrAdd(handlerAddress);
                                searchData = AddFunctionCandidate(handlerAddress, FoundBy.UnwindInfo, DiscoveryTrustLevel.Trusted, false);
                                searchData.FoundBySubType |= FoundBySubType.ScopeRecordHandler;
                            }

                            if (record.JumpTarget != 0)
                            {
                                var jumpAddress = record.JumpTarget + Module.Address;
                                GetOrAdd(jumpAddress);
                                searchData = AddFunctionCandidate(jumpAddress, FoundBy.UnwindInfo, DiscoveryTrustLevel.Trusted, false);
                                searchData.FoundBySubType |= FoundBySubType.ScopeRecordJumpTarget;
                            }
                        }
                    }
                }
            }
        }

        private void HydrateSymbols()
        {
            var symbolAddresses = new HashSet<long>();

            foreach (var symbol in Module.SymbolModule.EnumerateSymbols())
            {
                InstructionDiscoverySource searchData;

                if (symbol is MicrosoftPdbSymbol m)
                {
                    if (IsApparentCodeSymbol(m))
                    {
                        searchData = AddFunctionCandidate(symbol.Address, FoundBy.Symbol, DiscoveryTrustLevel.Trusted, false);
                        searchData.Symbol = symbol;

                        symbolAddresses.Add(searchData.Address);
                    }
                    else if (IsDataSymbol(m))
                    {
                        //If we've already added this item as a function (e.g. because there was an Export for it)
                        //change it to a data item

                        if (FunctionCandidates.TryGetValue(symbol.Address, out var existing))
                        {
                            FunctionCandidates.Remove(symbol.Address);

                            //We want to assert that we're the first person adding this, so that we ensure we keep
                            //any existing metadata stored on the candidate
                            DataCandidates.Add(symbol.Address, existing);
                        }

                        searchData = (InstructionDiscoverySource) AddCandidate(DataCandidates, null, symbol.Address, FoundBy.Symbol, DiscoveryTrustLevel.Trusted);
                        searchData.Symbol = symbol;

                        symbolAddresses.Add(searchData.Address);
                    }
                }
                else
                    throw new NotImplementedException();
            }

            OrderedSymbolAddresses = symbolAddresses.OrderBy(v => v).ToArray();
        }

        private bool IsApparentCodeSymbol(MicrosoftPdbSymbol symbol)
        {
            /* You can have symbols that refer to "code" and symbols that refer to "functions".
             * Our goal is to disassemble all "code" that exists within the assembly. Note that
             * some symbols reported as being "functions" aren't actually functions, but in fact
             * globals. */

            //Fast path: check the SymTagEnum kind (which we cache on our MicrosoftPdbSymbol)
            if (ShouldIgnoreSymbol(symbol))
                return false;

            //Regardless of what the symbol might have to say, if it doesn't have an address, we can't use it
            if (symbol.SafeDiaSymbol.RelativeVirtualAddress == null)
                return false;

            if (symbol.SafeDiaSymbol.Function == true)
                return true;

            if (symbol.SafeDiaSymbol.Code == true)
                return true;

            switch (symbol.SymTag)
            {
                case SymTagEnum.PublicSymbol:
                    return false;

                case SymTagEnum.Data:                
                    //At this point, if we haven't determined that it is code, assume it isn't
                    return false;

                //ntdll!RtlpHpVaMgrRangeCreate fails GetFunction but has SymTagEnum.Function
                case SymTagEnum.Function:
                    return true;

                default:
                    throw new NotImplementedException($"Don't know whether a symbol with tag {symbol.SymTag} is a function");
            }
        }

        private bool IsDataSymbol(MicrosoftPdbSymbol symbol)
        {
            //Fast path: check the SymTagEnum kind (which we cache on our MicrosoftPdbSymbol)
            if (ShouldIgnoreSymbol(symbol))
                return false;

            //Regardless of what the symbol might have to say, if it doesn't have an address, we can't use it
            if (symbol.SafeDiaSymbol.RelativeVirtualAddress == null)
                return false;

            switch (symbol.SymTag)
            {
                case SymTagEnum.PublicSymbol:
                case SymTagEnum.Data:
                    return true;

                default:
                    throw new NotImplementedException($"Don't know whether a symbol with tag {symbol.SymTag} is data");
            }
        }

        private bool ShouldIgnoreSymbol(MicrosoftPdbSymbol symbol)
        {
            switch (symbol.SymTag)
            {
                case SymTagEnum.Annotation:
                case SymTagEnum.ArrayType:
                case SymTagEnum.BaseType:
                case SymTagEnum.Compiland:
                case SymTagEnum.FunctionArgType:
                case SymTagEnum.FunctionType:
                case SymTagEnum.PointerType:
                case SymTagEnum.UDT:
                case SymTagEnum.Enum:
                case SymTagEnum.Typedef:
                case SymTagEnum.BaseClass:
                case SymTagEnum.VTable:
                    return true;

                default:
                    return false;
            }
        }

        private void HydratePatterns()
        {
            var tree = ByteSequenceTreeNode.BuildTree(x64ByteSequence.GetPatterns());

            var matches = tree.GetMatches(memoryStream.GetBuffer()).ToList();

            //RemoveBadPatternMatches(matches, memoryStream.GetBuffer());

            foreach (var match in matches)
            {
                var searchData = AddFunctionCandidate(match.Position + Module.Address, FoundBy.Pattern, DiscoveryTrustLevel.Trusted, false);

                searchData.ByteSequence = match.Sequence;
            }
        }

        private PriorityQueue<InstructionDiscoverySource, InstructionDiscoveryPriority> CreatePriorityQueue()
        {
            //PriorityQueue sorts by lowest to highest (i.e. a priority of 1 is greater than a priority of 2). In our queue however,
            //higher values should be considered better than lower values.
            return new PriorityQueue<InstructionDiscoverySource, InstructionDiscoveryPriority>(Comparer<InstructionDiscoveryPriority>.Create((a, b) =>
            {
                var result = a.CompareTo(b);

                if (result == -1)
                    return 1;

                if (result == 1)
                    return -1;

                return 0;
            }));
        }

        #endregion
        #region Process

        private void FindInstructions()
        {
            //Find all possible instructions based on trusted sources
            FindTrustedInstructions();

            /* Find instructions based on untrusted sources. Convert the
             * instructions to semi-trusted if they contain a call to something
             * that we know can be called (either because it was called by something
             * else that was trusted, or has a symbol/export associated with it).
             * If an item is untrusted, hold it in a cache and promote it to semi-trusted
             * if another untrusted/semi-trusted item is created that ends up calling to the
             * start of it. */
            //FindUntrustedInstructions();

            //Find instructions based on referenced addresses
            DiscoverReferencedAddrs();
        }

        private void FindTrustedInstructions()
        {
            currentPriorityQueue = trustedPriorityQueue;

            ProcessQueue();
        }

        private void FindUntrustedInstructions()
        {
            currentPriorityQueue = untrustedPriorityQueue;

            ProcessQueue();
        }

        private void ProcessQueue()
        {
            while (currentPriorityQueue.Count > 0)
            {
                var item = currentPriorityQueue.Dequeue();

                if (discoveredInstructions.ContainsKey(item.Address))
                {
                    /* Because we construct our resulting functions via a multi-step process, it doesn't matter who discovers what instructions
                     * initially. We'll ignore who was responsible for discovering each instruction anyway (a given discovery source may be just
                     * part of a function, and not represent a full function), and will use more general heuristics for trying to assign ownership
                     * of each instruction to an actual function */
                    item.Result = InstructionDiscoveryResult.Skipped;
                    continue;
                }

                try
                {
                    ctx.Add(item.Address);
                    DoDisassemble(item);

                    deferredSeen.Clear();

                    if (item.Result == InstructionDiscoveryResult.Success)
                    {
                        foreach (var value in deferredNewInstructions)
                        {
                            //If we're not interested in functions discovered by call, ignore
                            if (value.FoundBy == FoundBy.Call && !Options.HasFlag(PEMetadataSearchOptions.Call))
                                continue;

                            //Each item takes on the trust level of the top level function candidate we just disassembled
                            value.TrustLevel = item.TrustLevel;
                            RecordNewInstructions(value, value.DiscoveredCode);
                        }
                    }
                    else
                    {
                        //I don't think we need to remove it from the list of function candidates if its bad. It's normal to have potentially bad results in the function candidates
                        //map, and we handle that when determining whether a given function returns or not
                    }

                    deferredNewInstructions.Clear();
                }
                finally
                {
                    ctx.Remove(item.Address);
                }
            }
        }

        internal void DoDisassemble(InstructionDiscoverySource item)
        {
            var disasmResult = Disassembler.DisassembleCodeRegions(item.Address, ctx);

#if DEBUG
            if (disasmResult.IsSuccess)
            {
                foreach (var region in disasmResult.Regions)
                {
                    region.PhysicalStartAddress = Module.GetPhysicalAddress(region.StartAddress);
                    region.PhysicalEndAddress = Module.GetPhysicalAddress(region.EndAddress);
                }

                item.DiscoveredCode = disasmResult;
            }
#endif
            if (disasmResult.IsSuccess)
            {
                item.Result = InstructionDiscoveryResult.Success;

                if (item.TrustLevel == DiscoveryTrustLevel.Trusted)
                    RecordNewInstructions(item, disasmResult);
                else
                    deferredNewInstructions.Add(item);

                discoveredCode[item] = disasmResult;
            }
            else
            {
                item.Result = InstructionDiscoveryResult.Failure;

                //If it's a bad function, we can't trust whether the addresses we disassembled actually belong to
                //this function.
                badFunctions.Add((item, disasmResult));

                discoveredCode[item] = null;
            }
        }

        private void RecordNewInstructions(InstructionDiscoverySource item, NativeCodeRegionCollection function)
        {
            foreach (var instr in function.Instructions)
            {
                if (instr.Instruction.Mnemonic == Mnemonic.Call)
                {
                    if (instr.TryGetOperand(out var operand))
                    {
                        knownCallTargets.Add(operand);

                        if (!FunctionCandidates.ContainsKey(operand))
                        {
                            AddJumpOrCall(item, operand, FoundBy.Call, item.TrustLevel);
                        }
                    }
                }
                else if (instr.IsJump())
                {
                    //If it's an external jump to a function we haven't seen before
                    if (instr.TryGetOperand(out var operand))
                    {
                        knownJumpTargets.Add(operand);

                        if (!function.Contains(operand) && !FunctionCandidates.ContainsKey(operand))
                        {
                            //Supposedly there exists an external jump from this function to another function.
                            //If this external jump came from an UnwindData item that only covers a small chunk
                            //of a function, then this external jmp it's talking about is bogus, which we'll figure
                            //out when we dedupe the functions

                            AddJumpOrCall(item, operand, FoundBy.ExternalJmp, DiscoveryTrustLevel.SemiTrusted);
                        }
                    }
                    else
                    {
                        if (JumpTables.TryGetValue(instr.Address, out var jumpTable))
                        {
                            foreach (var target in jumpTable.Targets)
                                knownJumpTargets.Add(target);
                        }
                    }
                }

                discoveredInstructions[instr.Address] = instr;
            }
        }

        private void AddJumpOrCall(
            InstructionDiscoverySource item,
            long operand,
            FoundBy foundBy,
            DiscoveryTrustLevel trustLevel)
        {
            if (!ignoreMap.ContainsKey(operand) && Module.ContainsAddress(operand))
            {
                if (DataCandidates.TryGetValue(operand, out var existing) && existing is InstructionDiscoverySource c)
                {
                    //If we already think this is data, the fact we tried to call it isn't
                    //going to change our minds. For all we know this data isn't populated
                    //until runtime

                    c.FoundBy |= foundBy;
                }
                else
                {
                    var searchData = AddFunctionCandidate(operand, foundBy, trustLevel, true);
                    searchData.Caller = item;

                    //We don't fully trust external jumps to begin with, but if it turns out it was in an executable section, trust it fully
                    if (searchData.Section == null || searchData.Section.Value.Characteristics.HasFlag(IMAGE_SCN.MEM_EXECUTE))
                        item.TrustLevel = DiscoveryTrustLevel.Trusted;

                    //If we added this item as data, don't enqueue it
                    if (FunctionCandidates.ContainsKey(operand))
                        currentPriorityQueue.Enqueue(searchData, searchData.Priority);
                }
            }
        }

        internal void RemoveFromPriorityQueue(InstructionDiscoverySource item)
        {
            currentPriorityQueue.Remove(item, out _, out _);
        }

        private InstructionDiscoverySource AddFunctionCandidate(long address, FoundBy foundBy, DiscoveryTrustLevel trustLevel, bool call)
        {
            //If we don't 100% trust that this address points to code, if we find that it's not in an executable section, treat it as code
            var existing = AddCandidate(FunctionCandidates, trustLevel != DiscoveryTrustLevel.Trusted ? DataCandidates : null, address, foundBy, trustLevel);

            //Most FoundBy types we add immediately, prior to beginning. If we encounter a call, we need to update the priority,
            //which we can only do by removing and re-adding
            if (call)
            {
                if (currentPriorityQueue.Remove(existing, out _, out _))
                {
                    currentPriorityQueue.Enqueue(existing, existing.Priority);
                }
            }

            return existing;
        }

        private T AddCandidate<T>(
            Dictionary<long, T> dict,
            Dictionary<long, object> dataDict,
            long address,
            FoundBy foundBy,
            DiscoveryTrustLevel trustLevel)
        {
            if (!dict.TryGetValue(address, out var existing))
            {
                var item = new InstructionDiscoverySource(address, foundBy, trustLevel);

                var sectionIndex = PEFile.GetSectionContainingRVA((int) (address - Module.Address));

                if (sectionIndex != -1)
                    item.Section = PEFile.SectionHeaders[sectionIndex];
#if DEBUG
                item.PhysicalAddress = Module.GetPhysicalAddress(address);
#endif
                existing = (T) (object) item;

                if (dataDict != null && item.Section != null && !item.Section.Value.Characteristics.HasFlag(IMAGE_SCN.MEM_EXECUTE))
                    dataDict[address] = existing;
                else
                    dict[address] = existing;
            }
            else
            {
                ((InstructionDiscoverySource) (object) existing).FoundBy |= foundBy;
            }

            return existing;
        }

        private void DiscoverReferencedAddrs()
        {
            var addrs = new List<long>();

            foreach (var instr in discoveredInstructions.Values)
            {
                var icedInstr = instr.Instruction;

                switch (icedInstr.FlowControl)
                {
                    case FlowControl.Call:
                    case FlowControl.ConditionalBranch:
                    case FlowControl.UnconditionalBranch:
                    case FlowControl.IndirectBranch:
                        continue;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TryAdd(long addr)
                {
                    if (Module.ContainsAddress(addr))
                        addrs.Add(addr);
                }

                for (var i = 0; i < icedInstr.OpCount; i++)
                {
                    var kind = icedInstr.GetOpKind(i);

                    switch (kind)
                    {
                        case OpKind.Register:
                            break;

                        case OpKind.NearBranch16:
                            TryAdd(icedInstr.NearBranch16);
                            break;

                        case OpKind.NearBranch32:
                            TryAdd(icedInstr.NearBranch32);
                            break;

                        case OpKind.NearBranch64:
                            TryAdd((long) icedInstr.NearBranch64);
                            break;

                        case OpKind.FarBranch16:
                            TryAdd(icedInstr.FarBranch16);
                            break;

                        case OpKind.FarBranch32:
                            TryAdd(icedInstr.FarBranch32);
                            break;

                        case OpKind.Immediate8:
                            TryAdd(icedInstr.Immediate8);
                            break;

                        case OpKind.Immediate8_2nd:
                            TryAdd(icedInstr.Immediate8_2nd);
                            break;

                        case OpKind.Immediate16:
                            TryAdd(icedInstr.Immediate16);
                            break;

                        case OpKind.Immediate32:
                            TryAdd(icedInstr.Immediate32);
                            break;

                        case OpKind.Immediate64:
                            TryAdd((long) icedInstr.Immediate64);
                            break;

                        case OpKind.Immediate8to16:
                            TryAdd(icedInstr.Immediate8to16);
                            break;

                        case OpKind.Immediate8to32:
                            TryAdd(icedInstr.Immediate8to32);
                            break;

                        case OpKind.Immediate8to64:
                            TryAdd(icedInstr.Immediate8to64);
                            break;

                        case OpKind.Immediate32to64:
                            TryAdd(icedInstr.Immediate32to64);
                            break;

                        case OpKind.MemorySegSI:
                        case OpKind.MemorySegESI:
                        case OpKind.MemorySegRSI:
                        case OpKind.MemorySegDI:
                        case OpKind.MemorySegEDI:
                        case OpKind.MemorySegRDI:
                        case OpKind.MemoryESEDI:
                        case OpKind.MemoryESRDI:
                            break;

                        case OpKind.Memory:
                            TryAdd((long) icedInstr.MemoryDisplacement64);
                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle {nameof(OpKind)} '{kind}'");
                    }
                }
            }

            foreach (var addr in addrs)
            {
                if (!discoveredInstructions.ContainsKey(addr) && !DataCandidates.ContainsKey(addr) && addr != Module.Address)
                {
                    var searchData = AddFunctionCandidate(addr, FoundBy.RVA, DiscoveryTrustLevel.Untrusted, false);

                    if (searchData.Section == null || searchData.Section.Value.Characteristics.HasFlag(IMAGE_SCN.MEM_EXECUTE))
                        untrustedPriorityQueue.Enqueue(searchData, searchData.Priority);
                }
            }

            FindUntrustedInstructions();
        }

        #endregion
        #region Finalize

        private IList<NativeFunctionChunkBuilder> IdentifyFunctionChunks()
        {
            /* Trace instructions until we hit a terminating instruction.
             * The instructions collected then become a function chunk. Store
             * a map of instruction addresses to the instruction+chunk they belong to.
             * This will allow us to resolve jump targets later on. Each chunk should
             * record the conditional and unconditional jumps it takes outside of itself. */

            var chunkBuilders = new List<NativeFunctionChunkBuilder>();
            var currentInstructions = new List<ChunkedInstruction>();

            var chunkedInstructions = discoveredInstructions.Values.OrderBy(i => i.Address).Select(i =>
            {
                var instr = new ChunkedInstruction(i);

#if DEBUG
                instr.OriginalAddress = Module.GetPhysicalAddress(i.Address);
#endif

                return instr;
            }).ToArray();

            var chunkedInstrMap = chunkedInstructions.ToDictionary(i => i.Instruction.Address, i => i);

            //Simply run through all the instructions, splitting them out into separate
            //chunks each time we hit a terminating instruction
            for (var i = 0; i < chunkedInstructions.Length; i++)
            {
                var instr = chunkedInstructions[i];
                currentInstructions.Add(instr);

                if (IsTerminatingInstruction(instr, i, chunkedInstructions))
                {
                    chunkBuilders.Add(new NativeFunctionChunkBuilder(currentInstructions.ToList(), FunctionCandidates));
                    currentInstructions.Clear();
                }
            }

            //RtlRaiseStatus calls back into itself and doesn't have a terminating instruction
            if (currentInstructions.Count > 0)
            {
                chunkBuilders.Add(new NativeFunctionChunkBuilder(currentInstructions.ToList(), FunctionCandidates));
                currentInstructions.Clear();
            }

            /* Analyze all of the conditional/unconditional jump
             * instructions within each chunk and store the relationships
             * between different chunks */
            foreach (var chunkBuilder in chunkBuilders)
            {
                chunkBuilder.ResolveReferences(
                    chunkedInstrMap,
                    JumpTables,
                    exceptionUnwindMap,
                    Module.Address
                );
            }

            return chunkBuilders;
        }

        private bool IsTerminatingInstruction(ChunkedInstruction instruction, int i, ChunkedInstruction[] instructions)
        {
            switch (instruction.Instruction.Instruction.FlowControl)
            {
                case FlowControl.UnconditionalBranch:
                case FlowControl.IndirectBranch:
                case FlowControl.Return:
                    return true;

                default:
                    /* Processing interrupts is tricky. Sometimes IDA treats them as dead ends, otherwise it doesn't. In the case of an int 3,
                     * technically speaking if you had a debugger attached you could just step past it, so actually it does make sense that it
                     * would connect to the instructions after it. So even though ntdll!RtlCanonicalizeDomainName has an int 3 which is treated
                     * like a dead end that doesn't connect to the mov instruction directly after it, if you had a debugger attached, those two
                     * instructions probably *would* connect. */

                    if (i < instructions.Length - 1)
                    {
                        var nextInstruction = instructions[i + 1];

                        var currentEnd = instruction.Instruction.Address + instruction.Instruction.Bytes.Length;

                        //If we're 2 bytes and occupy 0x1000 and 0x1001 then the end is 0x1002 which is the start of the next one
                        if (currentEnd != nextInstruction.Instruction.Address)
                            return true;

                        /* Note that we can't split instructions at this stage based on whether or not there's a symbol associated with the
                         * next instruction. RtlInterlockedPopEntrySList and ExpInterlockedPopEntrySListResume don't connect together, they're
                         * just after each other, so if we split them up at this stage, we'll end up with chunk vertices that aren't connected,
                         * and will get split out into seaprate graphs.
                         *
                         * There's several key examples from ntdll of patterns of symbols inside of functions:
                         * - KiUserCallbackDispatcherContinue / RtlCallEnclaveReturn -> these are blocks that occur straight after a call. Therre's no jumps to them
                         * - ExpInterlockedPopEntrySListResume                       -> there's a jump to it from a subsequent block. But the previous block RtlpInterlockedPopEntrySList doesn't connect to it
                         * - ___entry_from_strcat_in_strcpy                          -> there's no jump to it from the strcpy before, but is jumped to from strcat later in the function
                         -*/

                        if (instruction.Instruction.Instruction.FlowControl == FlowControl.Interrupt)
                        {
                            //We know that the next instruction is right after us. If it ever gets jumped to from anywhere, assume that it doesn't belong to us

                            if (knownJumpTargets.Contains(nextInstruction.Instruction.Address))
                                return true;
                        }
                    }

                    return false;
            }
        }

        private void CollectFunctions(IList<NativeFunctionChunkBuilder> chunkBuilders)
        {
            /* Split the list of chunks out into separate graphs of chunks that are
             * interrelated. We then need to figure out these chunks are part of the same
             * function, or whether we have multiple distinct functions that jump to each other
             * (and may even potentially form a loop). We apply several heuristics to determine
             * whether we may need to split a graph of chunks:
             * - Are there any calls (globally) that go to the start of more than 1 chunk?
             * - How many chunks have 0 references going to them? (indicating they are root nodes)
             * - Do we have any symbols/exports that point to the start of a chunk?
             *
             * In creating these separate graphs, we also need to capture any unwind data
             * chunks that may be linked together as well
             * */
            var chunkGraphs = CreateChunkGraphs(chunkBuilders);

            ProcessChunkGraphs(chunkGraphs);
        }

        private ChunkGraph[] CreateChunkGraphs(IList<NativeFunctionChunkBuilder> chunkBuilders)
        {
            /* A graph consists of "vertices" (nodes) and "edges" (lines)
             * For each vertex, we want to say whether it's a conditional,
             * unconditional or exception-handler-related relationship */

            var processed = new Dictionary<NativeFunctionChunkBuilder, ChunkVertex>();
            var queue = new Queue<(NativeFunctionChunkBuilder builder, ChunkVertex vertex)>();

            var currentVertices = new List<IChunkVertex>();
            var chunkGraphs = new List<ChunkGraph>();

            foreach (var builder in chunkBuilders)
            {
                //If we've already processed this chunk builder as part of another graph, ignore
                if (processed.ContainsKey(builder))
                    continue;

                //It's a brand new chunk we haven't seen before

                var vertex = new ChunkVertex(builder);

                queue.Enqueue((builder, vertex));
                currentVertices.Add(vertex);
                processed.Add(builder, vertex);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    //Since each chunk is a contiguous set of instructions, we could have multiple xrefs that all lead to the same destination chunk. We only record the first xref to the target chunk

                    foreach (var refToThis in current.builder.XRefsToThis)
                    {
                        var chunk = refToThis.TheirInstr.Chunk;

                        //We may have a recursive reference
                        if (!processed.TryGetValue(chunk, out var existing))
                        {
                            vertex = new ChunkVertex(chunk);
                            vertex.AddRefFromThis(current.vertex, refToThis.Kind);
                            currentVertices.Add(vertex);
                            processed.Add(chunk, vertex);
                            queue.Enqueue((chunk, vertex));
                        }
                        else
                            existing.AddRefFromThis(current.vertex, refToThis.Kind);
                    }

                    foreach (var refFromThis in current.builder.XRefsFromThis)
                    {
                        var chunk = refFromThis.TheirInstr.Chunk;

                        //We may have a recursive reference
                        if (!processed.TryGetValue(chunk, out var existing))
                        {
                            vertex = new ChunkVertex(chunk);
                            current.vertex.AddRefFromThis(vertex, refFromThis.Kind);
                            currentVertices.Add(vertex);
                            processed.Add(chunk, vertex);
                            queue.Enqueue((chunk, vertex));
                        }
                        else
                            current.vertex.AddRefFromThis(existing, refFromThis.Kind);
                    }
                }

                //Ensure we make a copy of the list
                chunkGraphs.Add(new ChunkGraph(currentVertices.ToList()));
                currentVertices.Clear();
            }

            return chunkGraphs.ToArray();
        }

        private void ProcessChunkGraphs(ChunkGraph[] chunkGraphs)
        {
            var chunkRegions = new List<IMetadataRange>();

            var finalBuilders = new List<NativeFunctionChunkBuilder>();

            foreach (var chunkGraph in chunkGraphs)
            {
                if (chunkGraph.Vertices.Count == 1)
                {
                    //If there's only a single node in the graph, we know this is a function already

                    MakeFunction(chunkGraph, chunkRegions, finalBuilders);
                }
                else
                {
                    var results = ProcessComplexChunkGraph(chunkGraph);

                    foreach (var result in results)
                        MakeFunction(result, chunkRegions, finalBuilders);
                }
            }

            var ranges = GenerateMetadataRanges(chunkRegions);

            var metadataMap = ranges.ToDictionary(r => r.StartAddress, r => r);

            ApplyXRefs(finalBuilders, metadataMap);

            ranges = FindInstructionsBetweenRanges(ranges);

            Module.SetMetadata(ranges.ToArray());
        }

        void MakeFunction(ChunkGraph graph, List<IMetadataRange> chunkRegions, List<NativeFunctionChunkBuilder> finalBuilders)
        {
            var preDag = graph.Vertices
                .OrderBy(v =>
                {
                        //Returning false causes items to be brought to the front

                        var item = ((ChunkVertex) v).Builder.Item;

                    if (item == null)
                        return true;

                    if (item.FoundBySubType.HasFlag(FoundBySubType.ScopeRecordHandler))
                        return true; //Don't want to prioritize these

                        return false;
                })
                .ThenBy(v =>
                {
                    var item = ((ChunkVertex) v).Builder.Item;

                    if (item == null)
                        return true;

                        //Next, prioritize items that aren't just a straight call

                        return item.FoundBy != FoundBy.Call;
                })
                .Cast<IGraphVertex>()
                .ToArray();

            //Ostensibly, the first chunk should be the function start. However, if function A jumps to B, and we weren't able to detect that in fact B should be separate,
            //A and B may still be merged together in the final result. And if B has a higher starting address than A, it would erroneously be flagged as being the entrypoint of the function.
            //So, we solve this by constructing a DAG and using the function detected as being at the "top" of the tree
            var (_, sortedDag) = DirectedAcyclicGraph.Create<SimpleDagVertex<ChunkVertex>>(preDag, (o, e) => new SimpleDagVertex<ChunkVertex>((ChunkVertex) o, e));

            //Select an entry point
            var sortedVertices = sortedDag.Select(v => v.Original).ToList();

            var entry = sortedVertices.FirstOrDefault(v => v.Builder.Item != null && v.Builder.Item.FoundBy != FoundBy.Call) ?? sortedVertices[0];
            sortedVertices.Sort((a, b) => a.Builder.StartAddress.CompareTo(b.Builder.StartAddress));

            if (sortedVertices[0] != entry)
            {
                sortedVertices.Remove(entry);
                sortedVertices.Insert(0, entry);
            }

            var toRemove = new HashSet<IChunkVertex>();

            //Merge adjacent nodes
            for (var i = 0; i < sortedVertices.Count - 1; i++)
            {
                var current = sortedVertices[i];
                var next = sortedVertices[i + 1];

                if (current.Builder.EndAddress + 1 == next.Builder.StartAddress)
                {
                    current.Builder.Absorb(next.Builder);

                    toRemove.Add(next);
                }
            }

            sortedVertices.RemoveAll(v => toRemove.Contains(v));

            if (TrySplitMajorDisconnectedChunks(sortedVertices, toRemove, chunkRegions, finalBuilders))
                return;

            finalBuilders.AddRange(sortedVertices.Select(v => v.Builder));

            //If the function contains a loop between multiple symbols inside a function, we want to ensure that the "first" chunk is selected as the entry point.
            //If we have an exception handler that loops back to the beginning, we need to ensure that it is not visited first
            var chunks = sortedVertices.Select(v => v.Builder.ToChunk(FunctionCandidates, Module)).ToList();

            var function = new NativeFunction<InstructionDiscoverySource>(chunks);

            foreach (var region in function.AllRegions)
                chunkRegions.Add(region);
        }

        private bool TrySplitMajorDisconnectedChunks(
            List<ChunkVertex> sortedVertices,
            HashSet<IChunkVertex> deleted,
            List<IMetadataRange> functions,
            List<NativeFunctionChunkBuilder> finalBuilders)
        {
            //Treat any disconnected chunks with major symbols as being their own functions
            var symbolVertices = sortedVertices.Where(b =>
            {
                var item = b.Builder.Item;

                if (item == null)
                    return false;

                if (item.Symbol == null && item.Export == null)
                    return false;

                if (item.FoundBy.HasFlag(FoundBy.UnwindInfo))
                    return false;

                return true;
            }).ToArray();

            if (symbolVertices.Length <= 1)
                return false;

            PatchMergedChunkRefs(sortedVertices, deleted);

            var roots = symbolVertices.Select(v => v.Builder.StartAddress).ToHashSet();

            var currentGraph = new ChunkGraph(sortedVertices.Cast<IChunkVertex>().ToList());

            var split = splitter.ProcessAndSplitChunkGraphs(new[] { currentGraph }, a => roots.Contains(a));

            foreach (var item in split)
                MakeFunction(item, functions, finalBuilders);

            return true;
        }

        private void PatchMergedChunkRefs(List<ChunkVertex> sortedVertices, HashSet<IChunkVertex> deleted)
        {
            var builderToVertexMap = sortedVertices.ToDictionary(v => v.Builder, v => v);

            //First, patch up the references between each parent and their immediate children

            var flattenFromThis = new Dictionary<IChunkVertex, List<IChunkVertex>>();
            var flattenToThis = new Dictionary<IChunkVertex, List<IChunkVertex>>();

            foreach (var vertex in sortedVertices)
            {
                var refsToDelete = new List<(IChunkVertex Other, NativeXRefKind Kind)>();
                var refsToAdd = new List<(IChunkVertex Other, NativeXRefKind Kind)>();

                void ProcessRefs(HashSet<(IChunkVertex Other, NativeXRefKind Kind)> refs, Dictionary<IChunkVertex, List<IChunkVertex>> flatten)
                {
                    foreach (var @ref in refs)
                    {
                        var builder = ((ChunkVertex) @ref.Other).Builder;

                        var targetBuilder = builder.GetEffectiveChunk();

                        if (builder != targetBuilder)
                        {
                            //The builder mentioned in this ref was removed

                            refsToDelete.Add(@ref);

                            if (targetBuilder == vertex.Builder)
                            {
                                //The reference points to us. While we don't need a reference to anything to do with this deleted
                                //builder anymore, we DO need to potentially check the descendants of this builder and see if maybe we'll need to link
                                //to THEM.
                                if (!flatten.TryGetValue(vertex, out var list))
                                {
                                    list = new List<IChunkVertex>();
                                    flatten[vertex] = list;
                                }

                                list.Add(@ref.Other);
                            }
                            else
                            {
                                //The reference doesn't point to us, it points to some other chunk now. Add in a new reference
                                refsToAdd.Add((builderToVertexMap[targetBuilder], @ref.Kind));
                            }
                        }
                    }

                    if (refsToDelete.Count > 0)
                    {
                        foreach (var item in refsToDelete)
                            refs.Remove(item);

                        foreach (var item in refsToAdd)
                            refs.Add(item);
                    }
                }

                ProcessRefs(vertex.ExternalRefsFromThis, flattenFromThis);
                ProcessRefs(vertex.ExternalRefsToThis, flattenToThis);

                vertex.ClearEdges();
            }

            var visited = new HashSet<IChunkVertex>();

            IEnumerable<(IChunkVertex Other, NativeXRefKind Kind)> Flatten(IChunkVertex child, Func<IChunkVertex, HashSet<(IChunkVertex Other, NativeXRefKind Kind)>> getRefs)
            {
                if (!visited.Add(child))
                    yield break;

                foreach (var @ref in getRefs(child))
                {
                    if (deleted.Contains(@ref.Other))
                    {
                        foreach (var grandchild in Flatten(@ref.Other, getRefs))
                            yield return grandchild;
                    }
                    else
                    {
                        yield return @ref;
                    }
                }
            }

            //Now iterate over each vertex and flatten the hierarchy of any nodes that have been deleted.
            //e.g. if we have A -> B -> C -> D -> E, if A, B, C and D have all been merged, A needs to now reference E
            foreach (var item in flattenFromThis)
            {
                //Each entry in the list is an item that has been deleted

                foreach (var child in item.Value)
                {
                    var newRefsFromThis = Flatten(child, v => v.ExternalRefsFromThis).Where(v => v.Other != item.Key).Distinct().ToArray();
                    visited.Clear();

                    foreach (var newRefFromThis in newRefsFromThis)
                    {
                        item.Key.ExternalRefsFromThis.Add(newRefFromThis);
                        newRefFromThis.Other.ExternalRefsToThis.Add((item.Key, newRefFromThis.Kind));
                    }
                }
            }

            foreach (var item in flattenToThis)
            {
                //Each entry in the list is an item that has been deleted

                foreach (var child in item.Value)
                {
                    var newRefsToThis = Flatten(child, v => v.ExternalRefsToThis).Where(v => v.Other != item.Key).Distinct().ToArray();
                    visited.Clear();

                    foreach (var newRefFromThis in newRefsToThis)
                    {
                        item.Key.ExternalRefsToThis.Add(newRefFromThis);
                        newRefFromThis.Other.ExternalRefsFromThis.Add((item.Key, newRefFromThis.Kind));
                    }
                }
            }
        }

        private void ApplyXRefs(List<NativeFunctionChunkBuilder> builders, Dictionary<long, IMetadataRange> metadataMap)
        {
            foreach (var builder in builders)
                builder.ApplyXRefs(metadataMap);
        }

        private List<IMetadataRange> GenerateMetadataRanges(List<IMetadataRange> chunkRegions)
        {
            var knownMetadata = new List<IMetadataRange>();

            var is32Bit = PEFile.OptionalHeader.Magic == PEMagic.PE32;

            var regionMap = chunkRegions.ToDictionary(r => r.StartAddress, r => r);

            knownMetadata.AddRange(chunkRegions);

            foreach (var kv in DataCandidates)
            {
                IMetadataRange metadataRange;

                if (kv.Value is InstructionDiscoverySource c)
                    metadataRange = new DataMetadataRange(c, is32Bit);
                else if (kv.Value is IMetadataRange r)
                    metadataRange = r;
                else if (kv.Value is XfgMetadataInfo i)
                    metadataRange = new XfgMetadataRange(i, (INativeFunctionChunkRegion) regionMap[i.Owner]);
                else
                    throw new NotImplementedException($"Don't know how to handle value of type {kv.Value.GetType().Name}");

                knownMetadata.Add(metadataRange);
            }

            knownMetadata.Sort((a, b) => a.StartAddress.CompareTo(b.StartAddress));

            var ranges = new List<IMetadataRange>();

            var index = 0;

            IMetadataRange nextItem = null;

            if (knownMetadata.Count > 0)
            {
                nextItem = knownMetadata[index];
            }

            var headerEnd = PEFile.OptionalHeader.SizeOfHeaders;

            ranges.Add(new PEHeaderMetadataRange(Module.Address, headerEnd));

            var moduleEnd = Module.Address + PEFile.OptionalHeader.SizeOfImage;

            var rangeStart = Module.Address + headerEnd;

            JunkMetadataRange CreateJunkMetadata(long start, long end)
            {
                var startIndex = (int) (start - rangeStart);

                var length = (int) (end - start + 1);
                var buffer = new byte[length];
                memoryStream.Seek(headerEnd + startIndex, SeekOrigin.Begin);
                memoryStream.Read(buffer, 0, length);

                return new JunkMetadataRange(start, end, buffer);
            }

            IMetadataRange previousItem = null;

            for (var i = rangeStart; i < moduleEnd; i++)
            {
                if (nextItem != null)
                {
                    if (nextItem.StartAddress == i)
                    {
                        ranges.Add(nextItem);

                        i = nextItem.EndAddress;

                        index++;

                        previousItem = nextItem;

                        if (index < knownMetadata.Count - 1)
                            nextItem = knownMetadata[index];
                        else
                            nextItem = null;
                    }
                    else
                    {
                        //In ntdll, there's an export ntdll!SlashSystem32SlashString that DIA says is 16 bytes long. However, it actually points to the bytes 14 00 16 00. The actual \\SYSTEM32\\ string is 8 bytes up ahead - halfway into the symbol. 

                        if (previousItem != null && nextItem.StartAddress >= previousItem.StartAddress && previousItem is DataMetadataRange p && nextItem is DataMetadataRange n && nextItem.EndAddress <= previousItem.EndAddress)
                        {
                            p.AddChild(n);
                            index++;

                            //Don't update nextItem, since there could potentially be multiple children within its range
                            if (index < knownMetadata.Count - 1)
                                nextItem = knownMetadata[index];
                            else
                                nextItem = null;

                            i--;

                            continue;
                        }

                        Debug.Assert(nextItem.StartAddress > i);

                        var intermediateStart = i;
                        var intermediateEnd = nextItem.StartAddress - 1;

                        var intermediateLength = intermediateEnd - intermediateStart;

                        if (intermediateLength < 0)
                            throw new InvalidOperationException("Length should not be less than 0");

                        //If the length is 0, its just 1 byte
                        ranges.Add(CreateJunkMetadata(intermediateStart, intermediateEnd));

                        i = intermediateEnd;
                    }
                }
                else
                {
                    var end = moduleEnd;

                    ranges.Add(CreateJunkMetadata(i, end));

                    i = end;
                }
            }

            return ranges;
        }

        private List<IMetadataRange> FindInstructionsBetweenRanges(List<IMetadataRange> ranges)
        {
            /* If we've got
             *   Function A Chunk (1)
             *   Junk
             *   Function A Chunk (2)
             * Then this indicates that the Junk in-between may actually be part of Chunk (1) */

            var toRemove = new List<int>();

            for (var i = 1; i < ranges.Count - 1; i++)
            {
                if (ranges[i] is JunkMetadataRange junk)
                {
                    var previous = ranges[i - 1] as INativeFunctionChunkRegion;
                    var next = ranges[i + 1] as INativeFunctionChunkRegion;

                    var hadChanges = false;

                    if (previous != null && next != null && previous.Function.PrimaryMetadata == next.Function.PrimaryMetadata && previous.Function.PrimaryMetadata != null)
                        hadChanges = TryReplaceJunkBetweenRegions(junk, previous, next, ranges, i, toRemove);

                    if (!hadChanges && previous != null)
                        hadChanges = TryReplaceJunkAfterNonReturningCall(previous, junk, ranges, i, toRemove);
                }
            }

            if (toRemove.Count > 0)
            {
                var finalResults = new List<IMetadataRange>(ranges.Count);

                var skipped = 0;

                for (var i = 0; i < ranges.Count; i++)
                {
                    if (skipped < toRemove.Count && i == toRemove[skipped])
                    {
                        skipped++;
                        continue;
                    }

                    finalResults.Add(ranges[i]);
                }

                return finalResults;
            }

            return ranges;
        }

        private bool TryReplaceJunkBetweenRegions(
            JunkMetadataRange junk,
            INativeFunctionChunkRegion previous,
            INativeFunctionChunkRegion next,
            List<IMetadataRange> ranges,
            int i,
            List<int> toRemove)
        {
            //Assume anything starting with cc is garbage
            if (junk.Bytes[0] == 0xcc)
                return false;

            //A nop is garbage if its just that one instruction
            if (junk.Bytes.Length == 1 && junk.Bytes[0] == 0x90)
                return false;

            //Could the previous range possibly even flow into this next instruction?
            var lastInstr = previous.Instructions.Last();

            if (lastInstr.Instruction.FlowControl is FlowControl.UnconditionalBranch or FlowControl.Return)
                return false;

            var end = junk.EndAddress;

            var newInstrs = new List<INativeInstruction>();

            //This junk is probably some instructions!
            foreach (var junkInstr in Disassembler.EnumerateInstructions(junk.StartAddress))
            {
                newInstrs.Add(junkInstr);

                if (Disassembler.IP > end)
                    break;
            }

            if (newInstrs.Count > 0)
            {
                //We now need to patch the previous chunk, and add in any new jumps that we may have discovered
                previous.Chunk.AddInstructions(newInstrs);

                if (junk.EndAddress + 1 == Disassembler.IP)
                {
                    //We perfectly consumed all available bytes
                    toRemove.Add(i);

                    if (previous.EndAddress + 1 == next.StartAddress)
                    {
                        if (previous.Chunk.Absorb(next.Chunk))
                        {
                            //We merged previous and next together. Next is no longer needed. Replace next's index with previous
                            //in case we need to merge any more chunks, and flag next for removal
                            ranges[i + 1] = previous;
                            toRemove.Add(i + 1);
                        }
                    }
                }
                else
                    throw new NotImplementedException("Don't know how to shrink the bytes left in the junk after partially consuming some of the bytes");

                return true;
            }

            return false;
        }

        private bool TryReplaceJunkAfterNonReturningCall(
            INativeFunctionChunkRegion region,
            JunkMetadataRange junk,
            List<IMetadataRange> ranges,
            int i,
            List<int> toRemove)
        {
            //Sometimes you'll have a call to RtlRaiseStatus at the end of a region...followed by some additional instructions that finish off the region.
            //e.g. nop + ret, jmp, etc. This can only be the case if the region right after a piece of code is junk

            //Assume anything starting with cc is garbage
            if (junk.Bytes[0] == 0xcc)
                return false;

            if (junk.Bytes.All(b => b == 0x90 || b == 0xcc))
                return false;

            if (junk.Bytes.Length > 2 && junk.Bytes[0] == 0x90 && junk.Bytes[1] == 0xcc)
                return false;

            var lastInstr = region.Instructions.Last();

            switch (lastInstr.Instruction.FlowControl)
            {
                case FlowControl.Call:
                    break;

                case FlowControl.Interrupt:
                    if (lastInstr.Instruction.Immediate8 == WellKnownInterrupt.FailFast)
                    {
                        if (junk.Bytes.Length < 2)
                            return false;

                        //We'll accept a nop or a jmp
                        if (junk.Bytes[0] != 0x90 && junk.Bytes[0] != 0xe9)
                            return false;

                        if (junk.Bytes[1] == 0xcc)
                            return false;

                        //It's a nop followed by something (maybe a ret)
                        break;
                    }
                    else
                        throw new NotImplementedException($"Don't know how to handle interrupt {lastInstr.Instruction.Immediate8:X}");

                default:
                    return false;
            }

            var code = Disassembler.DisassembleCodeRegions(junk.StartAddress, ctx);

            //When we're running unit tests without Guard EH XFG info, this can result in XFG bytes being tagged as junk that we then try and disassemble into
            if (code.IsSuccess && code.Instructions.Length > 0)
            {
                if (code.Regions.Count > 1)
                    return false;

                region.Chunk.AddInstructions(code.Instructions);

                if (junk.EndAddress + 1 == Disassembler.IP)
                {
                    //We perfectly consumed all available bytes
                    toRemove.Add(i);
                }
                else
                {
                    //We could have encountered a jump, so the number of bytes read is the number of bytes encountered in the first region

                    var bytesRead = code.Regions[0].Length;

                    ranges[i] = new JunkMetadataRange(junk.StartAddress + bytesRead, junk.EndAddress, junk.Bytes.Skip(bytesRead).ToArray());

                    return true;
                }
            }

            return false;
        }

        private ChunkGraph[] ProcessComplexChunkGraph(ChunkGraph input)
        {
            var inputList = new[] {input};

            var splitByCall = SplitChunkGraphByCallees(inputList);
            var splitBySymbol = SplitChunkGraphBySymbols(splitByCall);

            //This must always be last so we can do special handling of interrupts
            //that may either be followed by more legit instructions, or followed
            //by the next function
            var splitByRoot = SplitChunkGraphByRootNodes(splitBySymbol);

            return splitByRoot;
        }

        private ChunkGraph[] SplitChunkGraphByCallees(ChunkGraph[] inputList)
        {
            splitter.Log("SplitChunkGraphByCallees");

            //Any chunk that is known to be called by another chunk should be in its own graph
            return splitter.ProcessAndSplitChunkGraphs(
                inputList,
                a => knownCallTargets.Contains(a)
            );
        }

        private ChunkGraph[] SplitChunkGraphBySymbols(ChunkGraph[] inputList)
        {
            splitter.Log("SplitChunkGraphBySymbols");

            return splitter.ProcessAndSplitChunkGraphs(
                inputList,
                a =>
                {
                    if (!FunctionCandidates.TryGetValue(a, out var item))
                        return false;

                    if (item.FoundBySubType.HasFlag(FoundBySubType.ScopeRecordHandler))
                        return false;

                    if (item.Symbol != null)
                    {
                        if (item.Symbol is MicrosoftPdbSymbol m)
                        {
                            //Things that are called are definitely functions (see SplitChunkGraphByCallees). Things
                            //with symbols may or may not be functions (ntdll!RtlInterlockedPopEntrySList is a function
                            //but ExpInterlockedPopEntrySListResume is code)
                            if (m.SafeDiaSymbol.Function == true)
                                return true;

                            //EtwNotificationUnregister is neither a Function, nor Code, but IS tagged as a function
                            if (m.SafeDiaSymbol.SymTag == SymTagEnum.Function)
                                return true;
                        }
                        else
                            throw new NotImplementedException();
                    }

                    return false;
                }
            );
        }

        private ChunkGraph[] SplitChunkGraphByRootNodes(ChunkGraph[] inputList)
        {
            splitter.Log("SplitChunkGraphByRootNodes");

            return splitter.ProcessAndSplitChunkGraphs(
                inputList,

                //Exception handlers may have no jumps going to them, but if they're listed as being related to a function in the unwind data, then the OS unwinder will know to jump to the exception handler itself
                //Additionally, RUNTIME_FUNCTION entries may begin with a cc instruction, in which case there isn't a direct jump to the beginning of that block per se
                a =>
                {
                    if (FunctionCandidates.TryGetValue(a, out var value))
                    {
                        if (value.FoundBy == FoundBy.RuntimeFunction)
                            return false;

                        //In RtlpUnwindEpilogue we get an area of code that is pointed to by the config data, but is actually unreachable. Its "rootedness" causes it to claim a bunch of instructions below it,
                        //which are normally jumped to from other areas of RtlpUnwindEpilogue. On that basis, we currently say that Config data on its own is insufficient evidence to justify the existence of
                        //a function
                        if (value.FoundBy == FoundBy.Config)
                            return false;
                    }

                    if (!knownJumpTargets.Contains(a) && !exceptionUnwindMap.ContainsKey(a))
                        return true;

                    return false;
                }
            );
        }

        #endregion
    }
}
