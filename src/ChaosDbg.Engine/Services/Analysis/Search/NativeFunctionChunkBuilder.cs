using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Disasm;
using Iced.Intel;
using PESpy;

namespace ChaosDbg.Analysis
{
    class NativeFunctionChunkBuilder
    {
        public class XRef
        {
            public ChunkedInstruction OurInstr { get; }

            public ChunkedInstruction TheirInstr { get; }

            public NativeXRefKind Kind { get; }

            public bool To { get; }

            internal XRef(
                ChunkedInstruction ourInstr,
                ChunkedInstruction theirInstr,
                NativeXRefKind kind,
                bool to)
            {
                OurInstr = ourInstr;
                TheirInstr = theirInstr;
                Kind = kind;
                To = to;
            }

            public override string ToString()
            {
                return Kind.ToString();
            }
        }

        public long StartAddress => instructions[0].Instruction.Address;

        public long EndAddress
        {
            get
            {
                var last = instructions.Last();

                return last.Instruction.Address + last.Instruction.Bytes.Length - 1;
            }
        }

        public HashSet<NativeFunctionChunkBuilder> ExternalRefsFromThis { get; } = new HashSet<NativeFunctionChunkBuilder>();

        public HashSet<NativeFunctionChunkBuilder> ExternalRefsToThis { get; } = new HashSet<NativeFunctionChunkBuilder>();

        public IEnumerable<XRef> XRefsFromThis => xrefs.Values.SelectMany(v => v).Where(v => !v.To).ToArray();
        public IEnumerable<XRef> XRefsToThis => xrefs.Values.SelectMany(v => v).Where(v => v.To).ToArray();

        private int[] jumps;
        private List<ChunkedInstruction> instructions;
        public InstructionDiscoverySource Item { get; }
        private Dictionary<INativeInstruction, List<XRef>> xrefs = new();

        public NativeFunctionChunkBuilder AbsorbedBy { get; set; }

        public void Absorb(NativeFunctionChunkBuilder other)
        {
            var owner = GetEffectiveChunk();

            other.AbsorbedBy = owner;

            owner.instructions.AddRange(other.instructions);
        }

        internal NativeFunctionChunkBuilder(
            List<ChunkedInstruction> instructions,
            Dictionary<long, InstructionDiscoverySource> functionCandidates)
        {
            this.instructions = instructions;

            var jumpList = new List<int>();

            for (var i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                instr.Chunk = this;

                if (instr.Instruction.IsJump())
                    jumpList.Add(i);
            }

            jumps = jumpList.ToArray();

            if (functionCandidates.TryGetValue(StartAddress, out var value))
                Item = value;
        }

        internal void ResolveReferences(
            Dictionary<long ,ChunkedInstruction> chunkedInstrMap,
            Dictionary<long, JumpTableMetadataRange> jumpTables,
            Dictionary<long, HashSet<ScopeTable.ScopeRecord>> exceptionUnwindMap,
            long moduleAddress)
        {
            ChunkedInstruction targetInstr;

            foreach (var jmpIndex in jumps)
            {
                var instr = instructions[jmpIndex];

                if (instr.Instruction.TryGetOperand(out var operand) && chunkedInstrMap.TryGetValue(operand, out targetInstr))
                {
                    if (targetInstr.Chunk == this)
                        continue;

                    Debug.Assert(targetInstr.Chunk != null);

                    NativeXRefKind refKind;

                    switch (instr.Instruction.Instruction.FlowControl)
                    {
                        case FlowControl.ConditionalBranch:
                            refKind = NativeXRefKind.ConditionalBranch;
                            break;

                        case FlowControl.UnconditionalBranch:
                            refKind = NativeXRefKind.UnconditionalBranch;
                            break;

                        case FlowControl.IndirectBranch:
                            refKind = NativeXRefKind.IndirectBranch;
                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle {nameof(FlowControl)} '{instr.Instruction.Instruction.FlowControl}'");
                    }

                    //Note that we don't currently store jumps _within_ a chunk
                    AddRefs(instr, targetInstr, refKind);
                }
                else
                {
                    if (jumpTables.TryGetValue(instr.Instruction.Address, out var jumpTable))
                    {
                        foreach (var addr in jumpTable.Targets)
                        {
                            if (chunkedInstrMap.TryGetValue(addr, out targetInstr))
                            {
                                AddRefs(instr, targetInstr, NativeXRefKind.JumpTable);
                            }
                        }
                    }
                }
            }

            //Now establish links between this chunk and any unwind metadata that may be linked to it

            var toInspect = new HashSet<long>();

            foreach (var instr in instructions)
            {
                var addr = instr.Instruction.Address;

                if (exceptionUnwindMap.TryGetValue(addr, out var sets))
                {
                    foreach (var set in sets)
                    {
                        toInspect.Add(set.BeginAddress + moduleAddress);
                        toInspect.Add(set.EndAddress + moduleAddress);

                        if (set.HandlerAddress > 1)
                            toInspect.Add(set.HandlerAddress + moduleAddress);

                        if (set.JumpTarget != 0)
                            toInspect.Add(set.JumpTarget + moduleAddress);
                    }

                    toInspect.Remove(addr);

                    foreach (var value in toInspect)
                    {
                        if (chunkedInstrMap.TryGetValue(value, out targetInstr))
                        {
                            AddRefs(instr, targetInstr, NativeXRefKind.UnwindInfo);
                        }
                    }

                    toInspect.Clear();
                }
            }
        }

        private void AddRefs(ChunkedInstruction instr, ChunkedInstruction targetInstr, NativeXRefKind refKind)
        {
            List<XRef> GetList(NativeFunctionChunkBuilder builder, ChunkedInstruction key)
            {
                if (!builder.xrefs.TryGetValue(key.Instruction, out var list))
                {
                    list = new List<XRef>();
                    builder.xrefs[key.Instruction] = list;
                }

                return list;
            }

            ExternalRefsFromThis.Add(targetInstr.Chunk);
            var fromList = GetList(this, instr);
            fromList.Add(new XRef(instr, targetInstr, refKind, false));

            targetInstr.Chunk.ExternalRefsToThis.Add(instr.Chunk);
            var toList = GetList(targetInstr.Chunk, targetInstr);
            toList.Add(new XRef(targetInstr, instr, refKind, true));
        }

        private NativeFunctionChunk<InstructionDiscoverySource> chunk;

        public NativeFunctionChunk<InstructionDiscoverySource> ToChunk(
            Dictionary<long, InstructionDiscoverySource> functionCandidates,
            PEMetadataModule module)
        {
            if (chunk == null)
            {
                chunk = new NativeFunctionChunk<InstructionDiscoverySource>(instructions.Select((v, i) => v.Instruction).ToList());

                var regionStarts = new List<(int index, InstructionDiscoverySource candidate)>();

                for (var i = 0; i < chunk.Instructions.Count; i++)
                {
                    var instr = chunk.Instructions[i];

                    if (functionCandidates.TryGetValue(instr.Address, out var candidate) && (candidate.Export != null || candidate.Symbol != null))
                    {
                        regionStarts.Add((i, candidate));
                    }
                }

                if (regionStarts.Count == 0)
                {
                    //Create one big region for the whole chunk
                    regionStarts.Add((0, Item));
                }
                else
                {
                    if (regionStarts[0].index != 0)
                    {
                        regionStarts.Insert(0, (0, null));
                    }
                }

                chunk.AddRegions(regionStarts);

#if DEBUG
                foreach (var region in chunk.Regions)
                {
                    region.PhysicalStartAddress = module.GetPhysicalAddress(region.StartAddress);
                    region.PhysicalEndAddress = module.GetPhysicalAddress(region.EndAddress);
                }
#endif
            }

            return chunk;
        }

        private Dictionary<INativeInstruction, XRefAwareNativeInstruction> xrefConversionMap = new();

        public XRefAwareNativeInstruction GetXRefAwareInstr(ChunkedInstruction instr, bool patchImmediately)
        {
            if (!xrefConversionMap.TryGetValue(instr.Instruction, out var xrefAwareInstr))
            {
                xrefAwareInstr = ((NativeInstruction) instr.Instruction).ToXRefAware();

                if (patchImmediately)
                {
                    var index = -1;

                    for (var i = 0; i < chunk.Instructions.Count; i++)
                    {
                        if (chunk.Instructions[i] == instr.Instruction)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index == -1)
                        throw new InvalidOperationException($"Couldn't find instruction '{instr.Instruction}'");

                    chunk.SetInstruction(index, xrefAwareInstr);
                }

                xrefConversionMap[instr.Instruction] = xrefAwareInstr;
            }

            return xrefAwareInstr;
        }

        public NativeFunctionChunkBuilder GetEffectiveChunk()
        {
            var current = this;

            while (current.AbsorbedBy != null)
                current = current.AbsorbedBy;

            return current;
        }

        public void ApplyXRefs(Dictionary<long, IMetadataRange> metadataMap)
        {
            for (var i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];

                if (xrefs.TryGetValue(instr.Instruction, out var xrefList))
                {
                    var xrefAwareInstr = GetXRefAwareInstr(instr, false);

                    var refsFromThis = new List<INativeXRefInfo>();
                    var refsToThis = new List<INativeXRefInfo>();

                    foreach (var item in xrefList)
                    {
                        var targetChunk = item.TheirInstr.Chunk.GetEffectiveChunk();

                        var targetXRefAwareInstr = targetChunk.GetXRefAwareInstr(item.TheirInstr, true);

                        var targetRegion = targetChunk.ToChunk(null, null).FindRegion(item.TheirInstr.Instruction.Address);

                        if (targetRegion == null)
                            throw new InvalidOperationException($"Couldn't find the chunk that instruction '{item.TheirInstr.Instruction}' belongs to");

                        var info = new NativeJumpXRefInfo(targetXRefAwareInstr, targetRegion, item.Kind);

                        if (item.To)
                            refsToThis.Add(info);
                        else
                            refsFromThis.Add(info);
                    }

                    xrefAwareInstr.RefsFromThis.AddRange(refsFromThis);
                    xrefAwareInstr.RefsToThis.AddRange(refsToThis.ToArray());

                    chunk.SetInstruction(i, xrefAwareInstr);
                }
                else
                {
                    if (instr.Instruction.Instruction.Mnemonic == Mnemonic.Call)
                    {
                        if (instr.Instruction.TryGetOperand(out var operand) && metadataMap.TryGetValue(operand, out var metadataTarget))
                        {
                            var xrefAwareInstr = GetXRefAwareInstr(instr, false);

                            xrefAwareInstr.RefsFromThis.Add(new NativeCallXRefInfo(metadataTarget));

                            chunk.SetInstruction(i, xrefAwareInstr);
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            if (Item != null)
            {
                var str = Item.Symbol?.ToString() ?? Item.Export?.ToString();

                if (str != null)
                    return str;
            }

            return instructions[0].OriginalAddress.ToString("X");
        }
    }
}
