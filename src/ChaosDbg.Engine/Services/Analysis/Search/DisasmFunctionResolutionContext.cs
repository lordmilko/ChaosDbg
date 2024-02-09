using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosDbg.Disasm;
using Iced.Intel;

namespace ChaosDbg.Analysis
{
    public class DisasmFunctionResolutionContext
    {
        private static readonly string[] noReturnFunctionsArray =
        {
            "exit",
            "xexit",
            "abort",
            "errx",
            "err",
            "std::terminate",
            "std::unexpected",
            "std::_Xlength_error",

            //msvcrt versions
            "?terminate@@YAXXZ",
            "?unexpected@@YAXXZ",

            "invoke_watson",
            "invalid_parameter_noinfo_noreturn",

            "longjmp",
            "longjmp_chk",
            "siglongjmp",
            "ExitProcess",
            "ExitThread",
            "assert",
            "wassert",
            "assert_fail",
            "stack_chk_fail",
            "stack_chk_fail_local",
            "stack_smash_handler",
            "report_gsfailure",
            "CxxThrowException",
            "AfxThrowMemoryException",
            "AfxThrowNotSupportedException",
            "AfxThrowInvalidArgException",
            "AfxThrowArchiveException",
            "AfxThrowFileException",
            "AfxThrowOleException",
            "com_raise_error",
            "com_issue_error",
            "StdThrow",

            //Unwind_RaiseException and Unwind_SjLj_RaiseException are returning
            "Unwind_Resume",
            "Unwind_SjLj_Resume",
            "Unwind_Resume_or_Rethrow",
            "Unwind_SjLj_Resume_or_Rethrow",

            "cxa_end_cleanup",
            "cxa_throw_bad_array_new_length",
            "cxa_bad_cast",
            "cxa_bad_typeid",
            "cxa_call_unexpected",
            "cxa_call_terminate",
            "cxa_deleted_virtual",
            "cxa_pure_virtual",
            "cxa_rethrow",
            "cxa_throw",

            "libc_fatal",
            "pthread_exit",

            //VB functions
            "vbaError",
            "vbaErrorOverflow",
            "vbaStopExe",
            "vbaFailedFriend",
            "vbaEnd",
            "vbaFPException",
            "vbaGenerateBoundsError",

            //NT Kernel functions
            "KeBugCheck",
            "KeBugCheck2",
            "KeBugCheckEx",
            "HalReturnToFirmware",
            "ExRaiseStatus",

            //Symbian functions
            "Leave__4Useri",

            //Go functions
            "go_runtime_error",
            "go_panic_msg",

            //armv7a gnueabi
            "std::__throw_logic_error",
            "std::__throw_bad_alloc",
            "std::__throw_bad_cast",
            "std::__throw_length_error",
            "std::__throw_out_of_range",
            "std::__throw_bad_function_call",
            "std::__throw_bad_weak_ptr",
            "std::__throw_system_error",

            //gcc v.3x mangling
            "ZSt25__throw_bad_function_callv",
            "ZSt24__throw_out_of_range_fmtPKcz",
            "ZSt19__throw_logic_errorPKc",
            "ZSt17__throw_bad_allocv",
            "ZSt16__throw_bad_castv",

            "cxxabiv1::__cxa_rethrow",
            "cxxabiv1::__cxa_throw",

            "runtime_goexit",
            "runtime_goexit1",

            //cygwin & rtos
            "assert_func",

            "; Android",
            "android_log_assert",

            //Ghidra
            "CxxThrowException@8",
            "CxxFrameHandler3",
            "crtExitProcess",
            "ExRaiseAccessViolation",
            "ExRaiseDatatypeMisalignment",
            "FreeLibraryAndExitThread",
            "quick_exit",
            "RpcRaiseException",
            "terminate"
        };

        private static HashSet<string> noReturnFunctions;

        static DisasmFunctionResolutionContext()
        {
            noReturnFunctions = new HashSet<string>();

            foreach (var item in noReturnFunctionsArray)
                noReturnFunctions.Add(item);
        }

        private InstructionSearcher searcher;
        private PEMetadataModule module => searcher.Module;
        private HashSet<long> resolutionStack = new HashSet<long>();
        private JumpTableParser jumpTableParser;

        internal DisasmFunctionResolutionContext(InstructionSearcher searcher, Stream stream)
        {
            this.searcher = searcher;
            jumpTableParser = new JumpTableParser(stream, searcher.Module.Address);
        }

        internal void Add(long address) => resolutionStack.Add(address);

        public void Remove(long address) => resolutionStack.Remove(address);

        public bool DoesFunctionReturn(long address, bool isJump)
        {
            if (resolutionStack.Contains(address))
                return true;

            resolutionStack.Add(address);

            try
            {
                if (searcher.FunctionCandidates.TryGetValue(address, out var existing))
                {
                    //We can't be caching stuff to do with jumps; one place might jump to a given function, another might call it.
                    //If we detect it as being jumped to first, we'll start going around telling everyone it doesn't return,
                    //even when it's being called. Whether something "returns" or not means different things based on whether
                    //you're jumping or not.
                    if (isJump)
                    {
                        //If a jmp instruction takes you to another known function, it's the end of a pathway
                        //as far as the current function is concerned. However, this also depends on what evidence
                        //we have for the existance of this item. If all we have is UnwindData, this means it's
                        //probably another part of the same function

                        if (existing.FoundBy != FoundBy.RuntimeFunction)
                            return false;
                    }

                    if (existing.DoesReturn != null)
                        return existing.DoesReturn.Value;

                    var result = DoesFunctionReturnForCandidate(existing);

                    existing.DoesReturn = result;

                    return result;
                }
                else
                {
                    if (!isJump)
                    {
                        /* Untrusted discovery sources won't have their instructions immediately added during
                         * DoDisassemble(). Instead, they'll be deferred and then processed after whatever call stack
                         * we're inside returns. Thus ensuring we don't throw away all the instructions we disassembled
                         * for this newly discovered function in order to see if it returns */

                        var pending = new InstructionDiscoverySource(address, FoundBy.Call, DiscoveryTrustLevel.Untrusted);

                        var sectionIndex = searcher.PEFile.GetSectionContainingRVA((int) (address - module.Address));

                        if (sectionIndex != -1)
                            pending.Section = searcher.PEFile.SectionHeaders[sectionIndex];

#if DEBUG
                        pending.PhysicalAddress = module.GetPhysicalAddress(pending.Address);
#endif

                        searcher.FunctionCandidates[address] = pending;

                        var result = DoesFunctionReturnForCandidate(pending);

                        pending.DoesReturn = result;

                        return result;
                    }
                }

                //If the address is a jump that we don't recognize, assume it's just another part of the function
                if (isJump)
                    return true;

                //We we've never heard of this address before

#if DEBUG
                if (searcher.Options.HasFlag(PEMetadataSearchOptions.Symbols))
                {
                    //Try and resolve the symbol straight from DbgHelp. If we get one, that means we erroneously determined that
                    //it's not a function when it is, and should assert
                    var symbol = searcher.Module.SymbolModule.GetSymbolFromAddress(address);

                    //If we get a symbol that has a displacement, then we didn't really get a symbol, we just got "this is close to something else"
                    Debug.Assert(symbol == null || symbol.Displacement > 0, $"Expected 0x{address:X} to not resolve to a known symbol, however got {symbol}. This indicates this symbol is in fact a function that was erroneously excluded.");
                }
#endif
                //In CLR modules we might be reading garbage as a call instruction.
                //Let it rip so that it eventually realizes this function is bad
                if (searcher.PEFile.Cor20Header != null)
                    return true;

                //We randomly hit this code path, which indicates that the order we process functions may impact things

#if DEBUG
                throw new NotImplementedException($"Don't know how to determine whether address 0x{address:X} that does not contain symbols ever returns");
#else
                return true;
#endif
            }
            finally
            {
                resolutionStack.Remove(address);
            }
        }

        private bool DoesFunctionReturnForCandidate(InstructionDiscoverySource existing)
        {
            if (existing.Symbol != null)
            {
                if (noReturnFunctions.Contains(existing.Symbol.Name))
                    return false;

                if (noReturnFunctions.Contains(existing.Symbol.DiaSymbol.UndecoratedName))
                    return false;

                //If we have symbol information indicating the function never returns. No need to disassemble it to check
                //this manually
                if (existing.Symbol.DiaSymbol.NoReturn)
                    return false;
            }

            var oldIP = searcher.Disassembler.IP;

            try
            {
                if (existing.Result == InstructionDiscoveryResult.None)
                {
                    //IDA Pro is capable of analyzing all code paths of a function: if all paths lead to non-returning functions,
                    //then this function itself is non-returning. We don't yet have such a capability

                    searcher.DoDisassemble(existing);

                    //We skipped the queue! Remove this item so we don't do it again
                    searcher.RemoveFromPriorityQueue(existing);
                }

                return DoInstructionsReturn(existing);
            }
            finally
            {
                searcher.Disassembler.IP = oldIP;
            }
        }

        internal bool ShouldProcessInstruction(long address)
        {
            if (searcher.discoveredInstructions.ContainsKey(address))
                return false;

            /* While processing this current stack of functions, we may have already encountered this address,
             * but not committed it to our discovered instructions yet, since we're not sure if we we can trust
             * these instructions yet or not. In fact, it's even possible for a single function to try and process
             * a given address twice, when you've got a jumps A -> B, C and B..C, if we process C first, then when
             * we start processing B, which lives right behind C, we'll see that we already discovered C when we treated
             * it like its own "region" pointed to by A previously. */

            if (!searcher.deferredSeen.Add(address))
                return false;

            return true;
        }

        //Implicitly, if we're calling this function we must be the one responsible for having added it in the first place;
        //otherwise we would have bailed out earlier by virtue of already having processed this instruction
        internal void Unsee(long address) => searcher.deferredSeen.Remove(address);

        internal bool IsInt3UnwindBlock(long regionAddr)
        {
            if (searcher.FunctionCandidates.TryGetValue(regionAddr, out var existing))
            {
                if (existing.RuntimeFunction != null)
                {
                    //ntdll!LdrGetProcedureAddressForCaller has an unwind block that just contains a CC after its ret finishes
                    return existing.RuntimeFunction.Length == 1;
                }
            }

            return false;
        }

        internal bool AllowFunctionSizeThresholdReached(long functionAddr)
        {
            //If we are disassembling a function with symbols, and we ran into another function, thats OK

            if (searcher.FunctionCandidates.TryGetValue(functionAddr, out var candidate))
            {
                if (candidate.FoundBy.HasFlag(FoundBy.Symbol) || candidate.FoundBy.HasFlag(FoundBy.Export) || candidate.FoundBy.HasFlag(FoundBy.UnwindInfo))
                    return true;
            }

            return false;
        }

        public long GetCodeRegionEndThreshold(long regionAddr)
        {
            //When it comes to items we have metadata info for, we do -1 to say that the last address
            //is the last address UP TO the end of the previous instruction

            var orderedSymbols = searcher.OrderedSymbolAddresses;

            if (orderedSymbols != null)
            {
                var symbolIndex = Array.IndexOf(orderedSymbols, regionAddr);

                if (symbolIndex == -1)
                {
                    //It's a function region. Just find the next address higher than it

                    for (var i = 0; i < orderedSymbols.Length; i++)
                    {
                        if (orderedSymbols[i] > regionAddr)
                        {
                            if (TryBinarySearchFirstGreaterThan(orderedSymbols, regionAddr, out var instrIndex))
                            {
                                if (i != instrIndex)
                                    throw new InvalidOperationException($"Binary search should not have returned index {i}");
                            }

                            return orderedSymbols[i] - 1;
                        }
                    }
                }
                else
                {
                    //It's a known function. Get the address of the next entity

                    if (symbolIndex < orderedSymbols.Length - 1)
                        return orderedSymbols[symbolIndex + 1] - 1;
                }
            }

            //There must always be a function region end threshold. At a minimum, it's the end of the current section and/or the module
            //itself

            //The address is either the last known address, or after the last known address.
            //The threshold will either be the end of the current section, or the end of the
            //current module if for some reason we can't identify the current section

            var rva = regionAddr - module.Address;

            var sectionIndex = searcher.PEFile.GetSectionContainingRVA((int) rva);

            if (sectionIndex != -1)
            {
                //TryGetOffset only needed when we have a physical address
                var section = searcher.PEFile.SectionHeaders[sectionIndex];
                var sectionEnd = section.VirtualAddress + section.VirtualSize;

                return module.Address + sectionEnd;
            }

            //We'll just say the threshold is the end of the module then
            return module.Address + searcher.PEFile.OptionalHeader.SizeOfImage;
        }

        internal bool TryGetJumpTable(long functionAddr, NativeCodeRegionDisassembler.RegionPathEnumerator enumerator, out long[] jumpTableTargets)
        {
            /* Because we don't "defer" registering our jump tables until we've accepted instructions as valid, if we're ignoring functions discovered
             * by "call", we may discard the instructions that discovered a jump table in the first place, without registering them as having ever been seen.
             * As a result, another function that touches the same instructions may come along that DOES have more evidence going for it than "call", and will
             * attempt to re-calculate and add the jump table, which we already added previously for the function we discarded */
            var lastInstr = enumerator.RootInstructions.Last().Address;

            if (searcher.JumpTables.TryGetValue(lastInstr, out var existing))
            {
                jumpTableTargets = existing.Targets;
                return true;
            }

            if (jumpTableParser.TryReadJumpTable(functionAddr, enumerator, out var jumpTable))
            {
                jumpTableTargets = jumpTable.Targets;
                searcher.DataCandidates.Add(jumpTable.StartAddress, jumpTable);
                searcher.JumpTables[lastInstr] = jumpTable;

                return true;
            }

            jumpTableTargets = default;
            return false;
        }

        private bool TryBinarySearchFirstGreaterThan(long[] arr, long target, out int resultIndex)
        {
            //https://stackoverflow.com/questions/6553970/find-the-first-element-in-a-sorted-array-that-is-greater-than-the-target
            int low = 0, high = arr.Length; // numElems is the size of the array i.e arr.size() 
            while (low != high)
            {
                int mid = (low + high) / 2; // Or a fancy way to avoid int overflow
                if (arr[mid] <= target)
                {
                    /* This index, and everything below it, must not be the first element
                     * greater than what we're looking for because this element is no greater
                     * than the element.
                     */
                    low = mid + 1;
                }
                else
                {
                    /* This element is at least as large as the element, so anything after it can't
                     * be the first element that's at least as large.
                     */
                    high = mid;
                }
            }
            /* Now, low and high both point to the element in question. */

            if (arr.Length == low)
            {
                resultIndex = default;
                return false;
            }

            resultIndex = low;
            return true;
        }

        private bool DoInstructionsReturn(InstructionDiscoverySource item)
        {
            if (!searcher.discoveredCode.TryGetValue(item, out var code))
                return false;

            if (code == null || !code.IsSuccess)
                return true;

            if (code.Regions.Count > 1)
                return true;

            if (code.Regions.Count == 0)
                return true;

            var lastInstr = code.Regions[0].Instructions.Last();

            if (lastInstr.Instruction.FlowControl == FlowControl.Interrupt)
                return false;

            switch (lastInstr.Instruction.Mnemonic)
            {
                case Mnemonic.Ret:
                case Mnemonic.Jmp:
                    return true;

                //gdi32full!std::vector<std::unique_ptr<ExceptionInformationWrapper>>::_Xlength() ends in garbage data:
                //  db 0Fh, 1Fh, 44h, 2 dup(0)
                //  db 0CCh
                //that gets treated as a "nop dword ptr[rax+rax]" instruction. IDA Pro knows somehow that this data is garbage
                //and that the function is __noreturn, despite the fact the fact the DIA symbol does not say NoReturn
                case Mnemonic.Nop:
                    return false;

                case Mnemonic.Call:
                    /* When a function ends with a call, it is said to not return. e.g. RtlRaiseStatus calls back into itself,
                     * RtlExitUserThread calls RtlExitUserProcess (so while RtlExitUserProcess DOES end in a ret, RtlExitUserThread
                     * doesn't and so is flagged as non-returning.
                     *
                     * We encounter issues however when we have exports in the middle of a function. e.g. RtlCallEnclave
                     * contains the export RtlCallEnclaveReturn right after the call to ZwCallEnclave. This creates a bit of
                     * a catch 22, because the whole issue we have here is that we don't actually _know_ the bounds of our
                     * functions at this point. How are we supposed to know that RtlCallEnclaveReturn isn't its own standalone
                     * function? Well, the fact that it starts literally right after a call instruction is very sus. RtlCallEnclave
                     * was cut short when it asked for the address of the next symbol after it. So when trying to interpret whether
                     * this function ever returns, we'll say that if we have another symbol literally right after the call, the call
                     * must be returning then, since if we didn't have that symbol, we wouldn't have stopped reading instructions from
                     * the function in the first place, and so Call _wouldn't_ have ended up being the last instruction */
                    if (!lastInstr.TryGetOperand(out var operand))
                        return true; //Calling into a random register or something is OK

                    //By default, the answer should be false. We'll only accept true if we know that there's a symbol at the next instruction address
                    var nextAddr = lastInstr.Address + lastInstr.Bytes.Length;

                    if (searcher.FunctionCandidates.TryGetValue(nextAddr, out var existing))
                        return true;

                    /* If our function is normally called, but somebody jumped to it, they may have already discovered some of our instructions (e.g. RtlpPrintErrorInformation -> DbgPrint)
                     * Thus, the fact we end in a call doesn't necessarily prove anything. Check whether the function we're calling returns (assuming its not this function again as is the case with RtlRaiseStatus).
                     * However, just because the function we're calling returns, that doesn't necessarily mean that _we_ return. RtlExitUserThread ends in a call to RtlExitUserProcess and then an 0xcc instruction.
                     * So first, we need to rule out the scenario that we already discovered the instruction that comes after this one */

                    if (!searcher.discoveredInstructions.ContainsKey(nextAddr) && !searcher.deferredSeen.Contains(nextAddr))
                        return false;

                    //OK, now check if we're calling a known returning function
                    if (searcher.FunctionCandidates.TryGetValue(operand, out var operandCandidate) && operandCandidate.Address != code.Address && operandCandidate.DoesReturn.Value)
                        return true;

                    return false;

                default:
                    //A "function" can end on literally any instruction when you consider that you can have multiple symbols within a given function.
                    //e.g. RtlCaptureContext (contains CcSaveNVContext), RtlpInterlockedPopEntrySList (which contains 3 other symbols)
                    return true;
            }
        }
    }
}
