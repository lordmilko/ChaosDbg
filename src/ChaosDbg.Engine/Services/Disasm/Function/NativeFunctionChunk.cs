using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using ChaosLib;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a contiguous collection of instructions that comprise part of a function.<para/>
    /// If multiple pieces of metadata apply to the instructions within this chunk, the instructions
    /// may be split into multiple <see cref="NativeFunctionChunkRegion"/> entities.
    /// </summary>
    public interface INativeFunctionChunk
    {
        long StartAddress { get; }

        /// <summary>
        /// Gets the address of the last byte that an instruction in this chunk occupies.
        /// </summary>
        long EndAddress { get; }

        INativeFunctionMetadata PrimaryMetadata { get; }

        IEnumerable<INativeFunctionMetadata> AllMetadata { get; }

        IReadOnlyList<INativeInstruction> Instructions { get; }

        IEnumerable<INativeFunctionChunkRegion> Regions { get; }

        public INativeFunction Function { get; }

        void AddInstructions(IList<INativeInstruction> instructions);

        /// <summary>
        /// Absorbs the instructions and regions of another chunk into this one.
        /// </summary>
        /// <param name="other">The other chunk to absorb.</param>
        /// <returns>True if the last region of this chunk was merged with the first region of <paramref name="other"/>, otherwise false.</returns>
        bool Absorb(INativeFunctionChunk other);
    }

    /// <summary>
    /// Represents a contiguous collection of instructions that comprise part of a function.<para/>
    /// If multiple pieces of metadata apply to the instructions within this chunk, the instructions
    /// may be split into multiple <see cref="NativeFunctionChunkRegion"/> entities.
    /// </summary>
    public class NativeFunctionChunk<T> : INativeFunctionChunk where T : INativeFunctionMetadata
    {
        public long StartAddress => Regions.First().StartAddress;

        /// <inheritdoc />
        public long EndAddress => Regions.Last().EndAddress;

#if DEBUG
        public long PhysicalStartAddress => Regions.First().PhysicalStartAddress;

        public long PhysicalEndAddress => Regions.First().PhysicalEndAddress;
#endif

        /// <inheritdoc cref="INativeFunctionChunk.PrimaryMetadata" />
        public T PrimaryMetadata => Regions.First().Metadata;

        /// <inheritdoc cref="INativeFunctionChunk.AllMetadata" />
        public IEnumerable<T> AllMetadata => Regions.Select(r => r.Metadata).Where(v => v != null).ToArray();

        public IReadOnlyList<INativeInstruction> Instructions { get; }

        /// <inheritdoc cref="INativeFunctionChunk.Regions" />
        public List<NativeFunctionChunkRegion<T>> Regions { get; } = new List<NativeFunctionChunkRegion<T>>();

        /// <inheritdoc cref="INativeFunctionChunk.Function" />
        public NativeFunction<T> Function { get; internal set; }

        private List<INativeInstruction> instructions;

        public NativeFunctionChunk(List<INativeInstruction> instructions)
        {
            Instructions = new ReadOnlyCollection<INativeInstruction>(instructions);
            this.instructions = instructions;
        }

        public void AddRegions(IList<(int index, T metadata)> regions)
        {
            if (Regions.Count == 0)
            {
                //Easy. Just add them all in

                for (var i = 0; i < regions.Count; i++)
                {
                    var currentStart = regions[i];

                    int length;

                    if (i < regions.Count - 1)
                        length = regions[i + 1].index - currentStart.index;
                    else
                        length = instructions.Count - currentStart.index;

                    var region = new NativeFunctionChunkRegion<T>(currentStart.metadata, instructions.AsSpan(currentStart.index, length), this);

                    Regions.Add(region);
                }
            }
            else
            {
                //Hard. Need to spit up the existing regions
                throw new System.NotImplementedException();
            }
        }

        public void AddInstructions(IList<INativeInstruction> instructions)
        {
            var lastRegionIndexStart = 0;

            for (var i = 0; i < Regions.Count - 1; i++)
                lastRegionIndexStart += Regions[i].Instructions.Length;

            this.instructions.AddRange(instructions);

            var lastRegion = Regions.Last();

            lastRegion.Instructions = this.instructions.AsSpan(lastRegionIndexStart);

#if DEBUG
            lastRegion.PhysicalEndAddress += instructions.Sum(i => i.Bytes.Length);
#endif
        }

        public void SetInstruction(int index, INativeInstruction instruction)
        {
            instructions[index] = instruction;
        }

        public NativeFunctionChunkRegion<T> FindRegion(long address)
        {
            foreach (var region in Regions)
            {
                if (region.Contains(address))
                    return region;
            }

            return null;
        }

        public bool Absorb(NativeFunctionChunk<T> other)
        {
            if (this == other)
                return false;

            if (EndAddress + 1 != other.StartAddress)
                throw new System.NotImplementedException();

            var didMergeRegion = false;

            var regionStartIndex = instructions.Count;

            //Merge all instructions into this chunk
            instructions.AddRange(other.Instructions);

            var skip = 0;

            if (other.Regions[0].Metadata == null)
            {
                var otherFirstRegionInstructions = other.Regions[0].Instructions;

                //The first region of other doesn't have any metadata. Merge it with the last region of this
                var lastRegion = Regions.Last();

                var lastRegionStart = regionStartIndex - lastRegion.Instructions.Length;

                lastRegion.Instructions = instructions.AsSpan(lastRegionStart, lastRegion.Instructions.Length + otherFirstRegionInstructions.Length);
                regionStartIndex += otherFirstRegionInstructions.Length;

                didMergeRegion = true;

#if DEBUG
                //Expand this region to the end address of the added instructions
                lastRegion.PhysicalEndAddress = other.Regions[0].PhysicalEndAddress;
#endif

                //Skip over the first region since we already handled it specially
                skip++;
            }

            foreach (var region in other.Regions.Skip(skip))
            {
                //Update the instructions on the regions we've absorbed to now point to us
                region.Instructions = instructions.AsSpan(regionStartIndex, region.Instructions.Length);
                regionStartIndex += region.Instructions.Length;

                //Update the chunk of each of the old chunk's regions
                region.Chunk = this;

                Regions.Add(region);
            }

            other.Regions.Clear();

            //Remove the chunk from the function
            Function.RemoveChunk(other);

            return didMergeRegion;
        }

        #region INativeFunctionChunk

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunctionMetadata INativeFunctionChunk.PrimaryMetadata => PrimaryMetadata;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<INativeFunctionMetadata> INativeFunctionChunk.AllMetadata => AllMetadata.Select(v => (INativeFunctionMetadata) v);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<INativeFunctionChunkRegion> INativeFunctionChunk.Regions => Regions;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunction INativeFunctionChunk.Function => Function;

        bool INativeFunctionChunk.Absorb(INativeFunctionChunk other)
        {
            if (other is NativeFunctionChunk<T> c)
                return Absorb(c);
            else
                throw new ArgumentException($"Expected a value of type '{GetType().Name}'", nameof(other));
        }

        #endregion

        public override string ToString()
        {
            var primaryMetadata = PrimaryMetadata;

            if (primaryMetadata != null)
                return primaryMetadata.ToString();

#if DEBUG
            if (PhysicalStartAddress != 0)
                return $"0x{PhysicalStartAddress:X} - 0x{PhysicalEndAddress:X} (Owner: {Function.PrimaryMetadata})";
#endif

            return $"0x{StartAddress:X} - 0x{EndAddress:X} (Owner: {Function.PrimaryMetadata})";
        }
    }
}
