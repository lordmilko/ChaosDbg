using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosLib.Metadata;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Provides facilities for disassembling a <see cref="NativeCodeRegionCollection"/> that approximately resembles a function from an <see cref="INativeDisassembler"/>.
    /// </summary>
    class NativeCodeRegionDisassembler
    {
        private HashSet<long> seenIPs = new HashSet<long>();
        private HashSet<long> allMissingJumps = new HashSet<long>();

        private List<NativeCodeRegion> regionsFound = new List<NativeCodeRegion>();

        //Stores all unresolved references within each region. Allows unwinding bad regions and iterating over the references between regions when processing jump tables
        private Dictionary<NativeCodeRegion, Dictionary<long, INativeInstruction>> regionToRefAddresses = new Dictionary<NativeCodeRegion, Dictionary<long, INativeInstruction>>();

        private INativeDisassembler nativeDisassembler;
        private long functionAddress;
        private DisasmFunctionResolutionContext context;

        //If we encounter bad instructions and cannot repair them, the entire function
        //is flagged as bad. We won't know its memory range; we'll need to rely on identifying
        //the surrounding entities to do this
        private bool badFunction;
        private NativeCodeDiscoveryError badFunctionReason;

        internal NativeCodeRegionDisassembler(
            INativeDisassembler nativeDisassembler,
            long address,
            DisasmFunctionResolutionContext context)
        {
            this.nativeDisassembler = nativeDisassembler;
            this.functionAddress = address;
            this.context = context;
        }

        public NativeCodeRegionCollection Disassemble()
        {
            regionsFound.Add(DisassembleCodeRegion(functionAddress));

            bool ShouldReturnBadFunction()
            {
                if (badFunction)
                {
                    if (badFunctionReason != NativeCodeDiscoveryError.FunctionSizeThresholdReached)
                        return true;

                    if (context.AllowFunctionSizeThresholdReached(functionAddress))
                    {
                        badFunction = false;
                        badFunctionReason = default;
                    }
                    else
                        return true;
                }

                return false;
            }

            if (ShouldReturnBadFunction())
                return new NativeCodeRegionCollection(functionAddress, regionsFound, badFunctionReason);

            while (allMissingJumps.Count > 0)
            {
                var regionAddr = allMissingJumps.First();

                var region = DisassembleCodeRegion(regionAddr);

                //Already seen
                if (region == null)
                    allMissingJumps.Remove(regionAddr);
                else
                    regionsFound.Add(region);

                if (ShouldReturnBadFunction())
                    return new NativeCodeRegionCollection(functionAddress, regionsFound, badFunctionReason);

                const int maxRegions = 4000;

                if (regionsFound.Count > maxRegions)
                    throw new InvalidOperationException($"Attempted to disassemble more than {maxRegions} regions in a single function. This indicates a runaway disassembly has occurred."); //fail, clearly we're missing something here cos there shouldnt be that many regions
            }

            regionsFound.RemoveAll(v => v == null);

            return new NativeCodeRegionCollection(functionAddress, regionsFound, NativeCodeDiscoveryError.None);
        }

        private NativeCodeRegion DisassembleCodeRegion(long regionAddr)
        {
            var regionMissingJumps = new Dictionary<long, INativeInstruction>();

            var instrs = new List<INativeInstruction>();

            //Allow int 3 if it's the first instruction
            var allowInt3 = true;

            //If we try and disassemble past an int 3 and then start encountering garbage data, rewind back to the last int 3
            //and signify that that was in fact the end of the function
            var lastInt3Index = -1;

            long functionRegionEndThreshold = -1;

            if (context != null)
                functionRegionEndThreshold = context.GetCodeRegionEndThreshold(regionAddr);

            foreach (var instr in nativeDisassembler.EnumerateInstructions(regionAddr))
            {
                if (context != null && !context.ShouldProcessInstruction(instr.Address))
                    break;

                //While there is technically an instruction whose bytes consists of all 0's, we don't expect
                //that should ever be the first instruction of a function, so we say if we're trying to disassemble
                //all 0's it probably isn't a function. If it is, leave it to the user to force reinterpret it themselves
                if (instrs.Count == 0 && instr.Bytes.All(b => b == '\0'))
                    break;

                //If we've either run into the next entity, or run out of room in the PE section or module,
                //we've got a big problem
                if (context != null && instr.IP > functionRegionEndThreshold)
                {
                    var callIndex = instrs.FindLastIndex(i => i.Instruction.Mnemonic == Mnemonic.Call);

                    if (callIndex != -1 && !context.AllowFunctionSizeThresholdReached(functionAddress))
                    {
                        RemoveInstructions(instrs, callIndex + 1, RemoveJumpTarget);
                    }
                    else
                    {
                        badFunction = true;
                        badFunctionReason = NativeCodeDiscoveryError.FunctionSizeThresholdReached;
                    }

                    break;
                }

                if (instr.Instruction.Code == Code.INVALID)
                {
                    //We're either going to repair the function or mark it s bad.
                    //Either way, we break
                    ProcessInvalid(regionAddr, instrs, instr, lastInt3Index, RemoveJumpTarget);
                    break;
                }

                var icedInstr = instr.Instruction;

                bool end = false;

                var didAllowInt3 = false;

                if (icedInstr.Code == Code.Int3)
                {
                    lastInt3Index = instrs.Count;

                    if (allowInt3 || !DoesFunctionLookFinished(instrs, RemoveJumpTarget))
                        didAllowInt3 = true;
                    else
                        end = true;
                }

                allowInt3 = false;

                if (end)
                    break;

                //If we're now processing random function regions, and we processed the function region that
                //sits directly after the current one first, then when we see that we've already seen this IP
                //before we know that we're at the end of the current region
                if (!seenIPs.Add(instr.IP))
                    break;

                instrs.Add(instr);

                RemoveJumpTarget(instr.IP);

                if (IsNonReturningCall(icedInstr))
                    break;

                void AddJumpTarget(long addr)
                {
                    if (!seenIPs.Contains(addr))
                    {
                        allMissingJumps.Add(addr);
                        regionMissingJumps[addr] = instr;
                    }
                }

                void RemoveJumpTarget(long addr)
                {
                    if (allMissingJumps.Contains(addr))
                    {
                        allMissingJumps.Remove(addr);
                        regionMissingJumps.Remove(addr);
                    }
                }

                switch (icedInstr.FlowControl)
                {
                    case FlowControl.ConditionalBranch:
                    case FlowControl.IndirectBranch:
                    case FlowControl.UnconditionalBranch:
                        ProcessJump(regionAddr, icedInstr, instrs, ref end, AddJumpTarget);
                        break;

                    case FlowControl.Interrupt:
                        if (!ProcessInterrupt(regionAddr, icedInstr, didAllowInt3, ref allowInt3, ref end))
                            end = true; //If there was an issue (which may not be the case), we want to record the bad region in a BadNativeFunction
                        break;

                    case FlowControl.Return:
                        end = true;
                        break;
                }

                if (end)
                    break;
            }

            if (instrs.Count == 0)
                return null;

            var region = new NativeCodeRegion(regionAddr, instrs);
            regionToRefAddresses[region] = regionMissingJumps;

            return region;
        }

        private bool DoesFunctionLookFinished(List<INativeInstruction> instrs, Action<long> removeJumpTarget)
        {
            if (instrs.Count == 0)
                return false;

            var lastInstr = instrs.Last();
            var lastIcedInstr = lastInstr.Instruction;

            switch (lastIcedInstr.FlowControl)
            {
                //e.g. ntdll!RtlStdReleaseStackTrace jumps over the int 3
                case FlowControl.ConditionalBranch:
                    return false;
            }

            switch (lastIcedInstr.Mnemonic)
            {
                //If the call never returned, we would have aborted the function region already...unless its something like ntdll!RtlRaiseStatus,
                //which never returns!
                case Mnemonic.Call:
                    switch (lastIcedInstr.Op0Kind)
                    {
                        case OpKind.NearBranch32:
                        case OpKind.NearBranch64:
                            if (functionAddress == (long) lastIcedInstr.NearBranchTarget)
                                return true;

                            //Well, we know the call wasn't terminating, so the int 3 must be legit
                            //e.g. ntdll!RtlpNotOwnerCriticalSection
                            return false;

                        case OpKind.Memory:
                            /* We couldn't detect whether the function ever returns manually because the call is
                             * against a memory address. It could be calling into kernel32!ExitProcess, but we'll
                             * give it the benefit of the doubt. If there were two int 3's in a row, we'll backtrack
                             * and then know this call was in fact the final instruction.
                             * e.g. mscoree!__crtExitProcess (only has one _ in DbgHelp) */
                            return false;
                    }
                    break;

                case Mnemonic.Int3:
                    /* We're calling this method because we're trying to figure out whether or not to include an int 3. However,
                     * if the previous instruction was also an int 3, that means we've got two int 3's in a row. Our assumption
                     * that the previous int 3 should be allowed was wrong. Remove it, and signify that it's the end of the function
                     * e.g. ntdll!RtlCultureNameToLCID */
                    context?.Unsee(instrs[instrs.Count - 1].Address);
                    instrs.RemoveAt(instrs.Count - 1);

                    //If we had a nop and gave it the benefit of the doubt, and then wound up with two int 3's, not only do we want to rewind
                    //the int 3's, but we should also get rid of this pointless nop
                    if (instrs.Count > 0 && instrs[instrs.Count - 1].Instruction.Mnemonic == Mnemonic.Nop)
                    {
                        context?.Unsee(instrs[instrs.Count - 1].Address);
                        instrs.RemoveAt(instrs.Count - 1);
                    }

                    return true;

                case Mnemonic.Nop:
                    //Seems a bit sus to me. Why would you have a nop followed by an int 3? Let's give it the benefit of the doubt however
                    //e.g. mscoree!GetMUILanguageNames when it calls  _CxxThrowException
                    return false;

                case Mnemonic.Ret:
                case Mnemonic.Retf:
                    return true;

                //This is way too sus. In the case of windows.storage!std::_Throw_tree_length_error, there was a call -> nop -> int 3 -> garbage
                case Mnemonic.Dec:
                {
                    for (var i = instrs.Count - 1; i >= 0; i--)
                    {
                        if (instrs[i].Instruction.Code == Code.Int3)
                        {
                            if (i > 0 && instrs[i - 1].Instruction.Mnemonic == Mnemonic.Nop)
                                i--;

                            RemoveInstructions(instrs, i, removeJumpTarget);

                            return true;
                        }
                    }

                    break;
                }
            }

            /* Ostensibly, we'd like to have special logic that checks whether we already have any regions, and if we have a ret in any region,
             * we're done. But we can't rely on that. iertutil!IEConfiguration_SetBrowserAppProfile moves a HRESULT into ebx, calls int 3 and
             * then jumps back into its normal code path */

#if DEBUG
            //In debug mode, we want to catch edge case scenarios where the function may in fact have already finished

            switch (lastIcedInstr.Mnemonic)
            {
                case Mnemonic.Mov:
                    return false;

                case Mnemonic.Lea: //clr!TailCallHelperStub. Odd, but its true
                    return false;

                //win32u!gdispatchtablevalues is not a function. However, it's marked as being Code by DIA and PublicCode by DbgHelp. It's actually just a global of type WORD.
                //In any case, IDA gets tripped up by this symbol as well, however it's a bit precarious because "add" is an extremely common instruction when disassembling
                //garbage. In this particular case, we're lucky in that a whole bunch of int 3's will follow, however we can't always guarantee that all globals erroneously
                //marked as being code will be followed by int 3
                case Mnemonic.Add:
                    return false;

                //ole32/combase!BreakIntoDebugger
                case Mnemonic.Sub:
                    return false;

                default:
                    //Commented out due to System.Data being full of symbols that don't actually point to functions
                    //throw new NotImplementedException($"Don't know how to handle mnemonic {lastIcedInstr.Mnemonic} in function {symbol}");
                    break;
            }
#endif
            //Function doesn't look finished yet to me!
            return false;
        }

        private bool IsNonReturningCall(Instruction icedInstr)
        {
            if (icedInstr.Mnemonic == Mnemonic.Call && context != null)
            {
                long target = 0;

                switch (icedInstr.Op0Kind)
                {
                    case OpKind.NearBranch32:
                    case OpKind.NearBranch64:
                        target = (long) icedInstr.NearBranchTarget;
                        break;

                    case OpKind.Memory:
                    case OpKind.Register:
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle operand of type {icedInstr.Op0Kind}");
                }

                if (target != 0 && !context.DoesFunctionReturn(target, false))
                    return true;
            }

            return false;
        }

        private bool ProcessInvalid(long regionAddr, List<INativeInstruction> instrs, INativeInstruction instr, int lastInt3Index, Action<long> removeJumpTarget)
        {
            //EnumerateInstructions() normally may only abort when multiple bad instructions in a row are encountered, however
            //we are not so lenient. We need to know that what we're reading is valid

            /* In rpcrt4!LRPC_ADDRESS::ProcessIO we have a call followed by an int 3, which we give the benefit of the doubt.
             * Unfortunately however, following this instruction is a bunch of junk data, and we start disassembling garbage
             * with INVALID instructions intermixed between. As such, we say say that if we start encountering bad data, rewind
             * back to the last int 3 (if we've had one) and assume that that was in fact the end of the function */

            if (lastInt3Index != -1)
            {
                RemoveInstructions(instrs, lastInt3Index, removeJumpTarget);
            }
            else
            {
                /* Now things get a bit precarious. There's three possibilities: we've made a huge mistake in our disassembly,
                 * there's actual bad instructions present, or there was a function call that never returns that wasn't followed
                 * by an int 3. msvcrt!raise calls _exit, after which there's a nop and some garbage instructions. IDA knows that exit
                 * doesn't return thanks to its configuration file noret.cfg. However, we need to be able to support any function,
                 * including those we may not know. */

                var foundEnd = false;

                for (var i = instrs.Count - 1; i >= 0; i--)
                {
                    if (instrs[i].Instruction.Mnemonic == Mnemonic.Nop)
                    {
                        if (i > 0)
                        {
                            if (instrs[i - 1].Instruction.Mnemonic == Mnemonic.Call)
                            {
                                //Assume that this was meant to be the last instruction
                                RemoveInstructions(instrs, i, removeJumpTarget);

                                foundEnd = true;
                                break;
                            }
                        }
                    }
                }

                if (!foundEnd)
                {
                    if (!TryRewindInvalidRegions(regionAddr))
                    {
                        //The entire memory range of this function cannot be trusted
                        //e.g. System.Data!?System_Data_SqlClient_TdsParser__cctor@@000001
                        badFunction = true;
                        badFunctionReason = NativeCodeDiscoveryError.InvalidInstruction;
                        return false;
                    }
                    else
                    {
                        //The current region is not valid
                        return false;
                    }
                }
            }

            return true;
        }

        private void ProcessJump(
            long regionAddr,
            Instruction icedInstr,
            List<INativeInstruction> instrs,
            ref bool end,
            Action<long> addJumpTarget)
        {
            long target = 0;

            var isUnconditionalJump = icedInstr.FlowControl is FlowControl.UnconditionalBranch or FlowControl.IndirectBranch;

            switch (icedInstr.Op0Kind)
            {
                //If we add support for additional operand kinds,
                //make sure we update RemoveInstructions() as well

                case OpKind.NearBranch32:
                case OpKind.NearBranch64:
                    target = (long) icedInstr.NearBranchTarget;
                    break;

                case OpKind.Memory:
                    //e.g. ntdll!LdrpCodeAuthzCheckDllAllowed
                    //We can't know where the jump goes to, so
                    //we treat this like a dead end
                    break;

                case OpKind.Register:
                    if (isUnconditionalJump && context != null && context.TryGetJumpTable(functionAddress, new RegionPathEnumerator(regionAddr, instrs, this), out var jumpTableTargets))
                    {
                        foreach (var item in jumpTableTargets)
                            addJumpTarget(item);

                        return;
                    }
                    else
                        end = true;
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle jump operand {icedInstr.Op0Kind}");
            }

            if (target != 0)
            {
                if (isUnconditionalJump)
                {
                    end = true;

                    //Don't treat this jump as part of the function if DoesFunctionReturn returns false
                    if (context == null || context.DoesFunctionReturn(target, true))
                        addJumpTarget(target);
                }
                else
                    addJumpTarget(target);
            }
            else
            {
                //If it's an indirect branch to some memory address, it's still unconditional, so it's all over
                if (isUnconditionalJump)
                    end = true;
            }
        }

        private bool ProcessInterrupt(long regionAddr, Instruction icedInstr, bool didAllowInt3, ref bool allowInt3, ref bool end)
        {
            if (icedInstr.Code == Code.Int3)
            {
                if (context != null && context.IsInt3UnwindBlock(regionAddr))
                    end = true;

                if (!didAllowInt3)
                    end = true;
            }
            else
            {
                switch (icedInstr.Op0Kind)
                {
                    case OpKind.Immediate8:
                        switch (icedInstr.Immediate8)
                        {
                            case WellKnownInterrupt.FailFast:
                                end = true; //Fast fail: immediately terminates the calling process with minimum overhead
                                break;

                            case WellKnownInterrupt.AssertionFailure: //assertion failure
                            case WellKnownInterrupt.Syscall: //syscall
                                break;

                            case WellKnownInterrupt.DebuggerPrompt:
                                //Typically an int 2Dh should be followed by an int 3, e.g. in ntdll!DebugPrompt
                                allowInt3 = true;
                                break;

                            default:
                                badFunction = true;
                                badFunctionReason = NativeCodeDiscoveryError.UnknownInterrupt;
                                return false;
                        }
                        break;

                    default:
                        //e.g. int1 has 0 operands, hence why it hits this outer default
                        badFunction = true;
                        badFunctionReason = NativeCodeDiscoveryError.UnknownInterrupt;

                        return false;
                }
            }

            return true;
        }

        private void RemoveInstructions(List<INativeInstruction> instrs, int index, Action<long> removeJumpTarget)
        {
            for (var i = index + 1; i < instrs.Count; i++)
            {
                var instr = instrs[i];

                context?.Unsee(instr.Address);

                long target = 0;

                switch (instr.Instruction.FlowControl)
                {
                    case FlowControl.ConditionalBranch:
                    case FlowControl.UnconditionalBranch:
                    case FlowControl.IndirectBranch:
                        switch (instr.Instruction.Op0Kind)
                        {
                            case OpKind.NearBranch32:
                            case OpKind.NearBranch64:
                                target = (long) instr.Instruction.NearBranchTarget;
                                break;
                        }
                        break;
                }

                if (target != 0)
                    removeJumpTarget(target);
            }

            instrs.RemoveRange(index, instrs.Count - index);
        }

        private bool TryRewindInvalidRegions(long regionAddr)
        {
            /* We've been lead astray. Most likely there was a call + int 3 that we tried to proceed
             * past at one point, but this has backfired spectacularly on us. That mistake may have been made
             * several regions away from now, so we would need to trace backwards from the current region to our previous
             * regions to find the point at which we made the mistake. Then, we'd also need to delete any regions we created
             * that depend on the regions we've created (and then the regions that depend on them, etc)
             * An example of this is windows.storage!wil::ActivityBase<CloudFileProvider,0,0,5,16777216,_TlgReflectorTag_Param0IsProviderType>::ActivityData<CloudFileProvider,_TlgReflectorTag_Param0IsProviderType>::SetStopResult
             * We don't see that wil::details::WilFailFast doesn't return, we spiral out of control */

            //We already know this region isn't salvagable, so our first point of call is to find the region that jumped to this region

            var regionsToRemove = new Stack<long>();

            regionsToRemove.Push(regionAddr);

            var didRepairAnything = false;

            var originalFunctionRegions = regionsFound.ToList();
            var originalRegionToRefAddresses = regionToRefAddresses.ToDictionary(kv => kv.Key, kv => kv.Value);

            while (regionsToRemove.Count > 0)
            {
                var currentRegion = regionsToRemove.Pop();

                var parentList = regionToRefAddresses.Where(kv => kv.Value.ContainsKey(currentRegion)).ToArray();

                if (parentList.Length > 1)
                    throw new NotImplementedException("There should never be more than one parent that was the first to reference a region");

                if (parentList.Length == 0)
                {
                    //We don't have this region anymore (or maybe never had it)
                    continue;
                }

                var parent = parentList[0];

                //Since we're assuming that the whole region may have been garbage, search from the front
                //rather than the back

                var oldInstructions = parent.Key.Instructions;

                var didRepairRegion = false;

                //The current region is on the chopping block. Either it's going to be repaired
                //(in which case we'll re-add it) or it's completely tained (in which case it should
                //stay removed)
                regionsFound.Remove(parent.Key);
                var newRefsFromRegion = regionToRefAddresses[parent.Key].ToDictionary(v => v.Key, v => v.Value);
                regionToRefAddresses.Remove(parent.Key);

                for (var i = 0; i < oldInstructions.Count; i++)
                {
                    var instr = oldInstructions[i];

                    if (instr.Instruction.Code == Code.Int3)
                    {
                        var newInstructions = oldInstructions.ToList();

                        RemoveInstructions(newInstructions, i, a =>
                        {
                            //Any jump targets we encounter while removing instructions
                            //also have to be removed

                            if (newRefsFromRegion.Remove(a))
                            {
                                //This address was indeed listed as a region

                                //If it hasn't been turned into a region yet, we can
                                //just remove it from the list. Otherwise, we need to locate
                                //and remove the region
                                if (!allMissingJumps.Remove(a))
                                    regionsToRemove.Push(a);
                            }
                        });

                        var newRegion = new NativeCodeRegion(parent.Key.StartAddress, newInstructions);

                        regionsFound.Add(newRegion);

                        regionToRefAddresses[parent.Key] = newRefsFromRegion;

                        didRepairRegion = true;
                        didRepairAnything = true;
                        break;
                    }
                }

                if (!didRepairRegion)
                {
                    //OK, this region is bad too then
                    regionsToRemove.Push(parent.Key.StartAddress);
                }
            }

            if (!didRepairAnything)
                return false;

            //If we removed _everything_ then this was a total fail!
            if (regionsFound.Count == 0)
            {
                //Restore the information we know about the bad function regions we removed
                //so that they can be reported in the BadNativeFunction
                regionsFound = originalFunctionRegions;
                regionToRefAddresses = originalRegionToRefAddresses;
                return false;
            }

            return true;
        }

        internal struct RegionPathEnumerator
        {
            private long rootRegionAddr;

            public List<INativeInstruction> RootInstructions { get; }

            private NativeCodeRegionDisassembler nativeFunctionDisassembler;
            private NativeCodeRegion currentRegion;
            private int currentRegionJumpIndex;

            public (IList<INativeInstruction> Instrs, int JumpIndex) Current
            {
                get
                {
                    if (currentRegion == null)
                        return (RootInstructions, RootInstructions.Count - 1);

                    return (currentRegion.Instructions, currentRegionJumpIndex);
                }
            }

            public RegionPathEnumerator(long rootRegionAddr, List<INativeInstruction> rootInstrs, NativeCodeRegionDisassembler nativeFunctionDisassembler)
            {
                this.rootRegionAddr = rootRegionAddr;
                RootInstructions = rootInstrs;
                this.nativeFunctionDisassembler = nativeFunctionDisassembler;
                currentRegion = null;
                currentRegionJumpIndex = 0;
            }

            public bool MoveNext()
            {
                var currentAddr = currentRegion?.StartAddress ?? rootRegionAddr;

                foreach (var kv in nativeFunctionDisassembler.regionToRefAddresses)
                {
                    if (kv.Value.TryGetValue(currentAddr, out var instr))
                    {
                        currentRegion = kv.Key;
                        currentRegionJumpIndex = kv.Key.Instructions.IndexOf(instr);
                        Debug.Assert(currentRegionJumpIndex != -1);
                        return true;
                    }
                }

                return false;
            }

            public void Reset()
            {
                currentRegion = null;
            }
        }
    }
}
