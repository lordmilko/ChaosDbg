using System.Diagnostics;
using ChaosDbg.Analysis;
using ChaosLib;

namespace ChaosDbg.Disasm
{
    public interface INativeFunctionChunkRegion : IMetadataRange
    {
        /// <summary>
        /// Gets the address of the last byte that an instruction in this region occupies.
        /// </summary>
        new long EndAddress { get; }

        /// <summary>
        /// Gets the metadata associated with this sequence of instructions within an <see cref="INativeFunctionChunk"/>.<para/>
        /// Typically, each chunk will contain only a single <see cref="INativeFunctionChunkRegion"/>, however when a function contains
        /// unwind data or multiple exports that point to various parts of the function, there may be more than one region present.
        /// </summary>
        INativeFunctionMetadata Metadata { get; }

        Span<INativeInstruction> Instructions { get; }

        INativeFunctionChunk Chunk { get; }

        INativeFunction Function { get; }

        public bool Contains(long address);
    }

    /// <summary>
    /// Represents a collection of instructions within a <see cref="NativeFunctionChunk"/> that are associated with a piece of metadata.
    /// </summary>
    public class NativeFunctionChunkRegion<T> : INativeFunctionChunkRegion where T : INativeFunctionMetadata
    {
        public long StartAddress => Instructions.First().Address;

        /// <inheritdoc />
        public long EndAddress
        {
            get
            {
                if (Instructions.Length > 0)
                {
                    var lastInstr = Instructions.Last();

                    //We do -1 because otherwise the end address will be the next address AFTER the end.
                    //i.e. if we occupy bytes 0x1000 and 0x1001, the end address should be 0x1001, not
                    //0x1002
                    return lastInstr.Address + lastInstr.Bytes.Length - 1;
                }
                else
                    return StartAddress;
            }
        }

#if DEBUG
        public long PhysicalStartAddress { get; set; }

        public long PhysicalEndAddress { get; set; }
#endif

        /// <inheritdoc cref="INativeFunctionChunkRegion.Metadata" />
        public T Metadata { get; }

        public Span<INativeInstruction> Instructions { get; internal set; }

        /// <inheritdoc cref="INativeFunctionChunkRegion.Chunk" />
        public NativeFunctionChunk<T> Chunk { get; internal set; }

        /// <inheritdoc cref="INativeFunctionChunkRegion.Function" />
        public NativeFunction<T> Function => Chunk.Function;

        internal NativeFunctionChunkRegion(T metadata, Span<INativeInstruction> instructions, NativeFunctionChunk<T> chunk)
        {
            Metadata = metadata;
            Instructions = instructions;
            Chunk = chunk;
        }

        public bool Contains(long address) =>
            address >= StartAddress && address <= EndAddress;

        #region INativeFunctionChunkRegion

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunctionMetadata INativeFunctionChunkRegion.Metadata => Metadata;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunctionChunk INativeFunctionChunkRegion.Chunk => Chunk;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INativeFunction INativeFunctionChunkRegion.Function => Chunk.Function;

        #endregion

        public override string ToString()
        {
            if (Metadata != null)
                return Metadata.ToString();

#if DEBUG
            if (PhysicalStartAddress != 0)
                return $"0x{PhysicalStartAddress:X} - 0x{PhysicalEndAddress:X} (Owner: {Function.PrimaryMetadata})";
#endif

            return $"0x{StartAddress:X} - 0x{EndAddress:X} (Owner: {Function.PrimaryMetadata})";
        }
    }
}
