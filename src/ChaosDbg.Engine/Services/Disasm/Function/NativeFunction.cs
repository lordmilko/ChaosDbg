using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a complete disassembled native function.
    /// </summary>
    public interface INativeFunction
    {
        /// <summary>
        /// Gets the start address of the function.
        /// </summary>
        public long Address { get; }

        /// <summary>
        /// Gets the separate regions within the PE containing contiguous collections of instructions that pertain to this function.
        /// </summary>
        public IEnumerable<INativeFunctionChunk> Chunks { get; }

        /// <summary>
        /// Gets the primary identifying piece of metadata that pertains to this function.
        /// </summary>
        public INativeFunctionMetadata PrimaryMetadata { get; }

        /// <summary>
        /// Gets all pieces of metadata contained within this function.
        /// </summary>
        public IEnumerable<INativeFunctionMetadata> AllMetadata { get; }

        /// <summary>
        /// Gets the metadata regions contained within each of this functions <see cref="Chunks"/>.
        /// </summary>
        public IEnumerable<INativeFunctionChunkRegion> AllRegions { get; }

        public IEnumerable<INativeInstruction> AllInstructions { get; }
    }

    /// <summary>
    /// Represents a complete disassembled native function.
    /// </summary>
    public class NativeFunction<T> : INativeFunction where T : INativeFunctionMetadata
    {
        /// <inheritdoc />
        public long Address => Chunks.First().StartAddress;

#if DEBUG
        public long PhysicalAddress => Chunks.First().PhysicalStartAddress;
#endif

        //Strictly speaking these members are not the same as those in the interface, hence why we need to specify the doc path

        /// <inheritdoc cref="INativeFunction.Chunks" />
        public IReadOnlyList<NativeFunctionChunk<T>> Chunks { get; }

        /// <inheritdoc cref="INativeFunction.PrimaryMetadata" />
        public T PrimaryMetadata => Chunks.First().PrimaryMetadata;

        /// <inheritdoc cref="INativeFunction.AllMetadata" />
        public IEnumerable<T> AllMetadata => Chunks.SelectMany(c => c.AllMetadata).ToArray();

        /// <inheritdoc cref="INativeFunction.AllRegions" />
        public IEnumerable<NativeFunctionChunkRegion<T>> AllRegions => Chunks.SelectMany(c => c.Regions).ToArray();

        public IEnumerable<INativeInstruction> AllInstructions => Chunks.SelectMany(c => c.Instructions).ToArray();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<NativeFunctionChunk<T>> chunks;

        internal NativeFunction(List<NativeFunctionChunk<T>> chunks)
        {
            foreach (var chunk in chunks)
                chunk.Function = this;

            this.chunks = chunks;
            Chunks = new ReadOnlyCollection<NativeFunctionChunk<T>>(chunks);
        }

        internal void RemoveChunk(NativeFunctionChunk<T> chunk)
        {
            chunks.Remove(chunk);
            chunk.Function = null;
        }

        #region INativeFunction

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<INativeFunctionChunk> INativeFunction.Chunks => Chunks.Select(v => (INativeFunctionChunk) v);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunctionMetadata INativeFunction.PrimaryMetadata => PrimaryMetadata;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<INativeFunctionMetadata> INativeFunction.AllMetadata => AllMetadata.Select(v => (INativeFunctionMetadata) v);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IEnumerable<INativeFunctionChunkRegion> INativeFunction.AllRegions => AllRegions;

        #endregion

        //Implicitly we must have a primary metadata, else we wouldn't have discovered the chunks of this function in the first place
        public override string ToString() => PrimaryMetadata.ToString();
    }
}
