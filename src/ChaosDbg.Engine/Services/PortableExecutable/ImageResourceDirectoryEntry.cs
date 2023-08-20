using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents an entry contained within an IMAGE_RESOURCE_DIRECTORY that either points to an IMAGE_RESOURCE_DATA_ENTRY, or yet another IMAGE_RESOURCE_DIRECTORY.
    /// </summary>
    public interface IImageResourceDirectoryEntry
    {
        /// <summary>
        /// Gets the 32 bits that were specified for the name or ID union.<para/>
        /// If the left-most bit is specified, the right-most 31 bits specify the offset to the name. Otherwise, the right-most 16 bits are the ID of the entry.
        /// </summary>
        BitVector32 NameOrId { get; } //A BitVector32 for better debugger display

        /// <summary>
        /// Gets whether the left-most bit in <see cref="NameOrId"/> is set, indicating that this is a named resource directory entry.
        /// </summary>
        bool IsName { get; }

        /// <summary>
        /// Gets the offset to the <see cref="ImageResourceDirectoryString"/> entry. Only applies when <see cref="IsName"/> is true.
        /// </summary>
        int NameOffset { get; }

        /// <summary>
        /// Gets the ID of the entry. Only applies when <see cref="IsName"/> is false.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the 32 bits that were specified for the offset union.<para/>
        /// If the left-mode bit is specified, the right-most 31 bits specify the offset to another IMAGE_RESOURCE_DIRECTORY.
        /// Otherwise, the entire offset is an offset to an IMAGE_RESOURCE_DATA_ENTRY structure. 
        /// </summary>
        BitVector32 Offset { get; }

        /// <summary>
        /// Gets the offset to the IMAGE_RESOURCE_DATA_ENTRY this entry points to. Only applies when <see cref="IsDirectory"/> is false.
        /// </summary>
        int OffsetToData { get; }

        /// <summary>
        /// Gets whether the value pointed to by <see cref="Offset"/> is another IMAGE_RESOURCE_DIRECTORY. If false, the offset points to an IMAGE_RESOURCE_DATA_ENTRY.
        /// </summary>
        bool IsDirectory { get; }

        /// <summary>
        /// Gets the offset to the IMAGE_RESOURCE_DIRECTORY_ENTRY this entry points to. Only applies when <see cref="IsDirectory"/> is true.
        /// </summary>
        int OffsetToDirectory { get; }
    }

    /// <summary>
    /// Represents an entry contained within an IMAGE_RESOURCE_DIRECTORY that either points to an IMAGE_RESOURCE_DATA_ENTRY, or yet another IMAGE_RESOURCE_DIRECTORY.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct ImageResourceDirectoryEntry : IImageResourceDirectoryEntry
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                if (IsName)
                {
                    builder.Append("[Name] Offset =");
                    builder.Append(NameOffset);
                }
                else
                {
                    builder.Append("[Id] Value = ");
                    builder.Append(Id);
                }

                builder.Append(", ");

                if (IsDirectory)
                {
                    builder.Append("[Directory] Offset =");
                    builder.Append(OffsetToDirectory);
                }
                else
                {
                    builder.Append("[Data] Offset =");
                    builder.Append(OffsetToData);
                }

                return builder.ToString();
            }
        }

        /// <inheritdoc />
        public BitVector32 NameOrId { get; } //A BitVector32 for better debugger display

        /// <inheritdoc />
        public bool IsName => ((NameOrId.Data >> 31) & 1) == 1; //Check if the left-most bit is set

        /// <inheritdoc />
        public int NameOffset => NameOrId.Data & 0x7FFFFFFF; //Remove the left-most bit and retain the other 31

        /// <summary>
        /// Gets the ID of the entry. Only applies when <see cref="IsName"/> is false.
        /// </summary>
        public int Id => NameOrId.Data & 0x0000FFFF; //Remove the left-most 16 bits and retain the right-most 16

        /// <inheritdoc />
        public BitVector32 Offset { get; } //A BitVector32 for better debugger display

        /// <inheritdoc />
        public int OffsetToData => Offset.Data;

        /// <inheritdoc />
        public bool IsDirectory => ((Offset.Data >> 31) & 1) == 1;

        /// <inheritdoc />
        public int OffsetToDirectory => Offset.Data & 0x7FFFFFFF;

        public ImageResourceDirectoryEntry(PEBinaryReader reader)
        {
            NameOrId = new BitVector32(reader.ReadInt32());
            Offset = new BitVector32(reader.ReadInt32());
        }
    }
}
