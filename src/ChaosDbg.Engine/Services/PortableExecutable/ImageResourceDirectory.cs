namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents the IMAGE_RESOURCE_DIRECTORY structure. Immediately following this structure
    /// are <see cref="NumberOfNamedEntries"/> + <see cref="NumberOfIdEntries"/> IMAGE_RESOURCE_DIRECTORY_ENTRY structures.
    /// </summary>
    public interface IImageResourceDirectory
    {
        uint Characteristics { get; }
        uint TimeDateStamp { get; }
        ushort MajorVersion { get; }
        ushort MinorVersion { get; }
        ushort NumberOfNamedEntries { get; }
        ushort NumberOfIdEntries { get; }
    }

    /// <summary>
    /// Represents the IMAGE_RESOURCE_DIRECTORY structure. Immediately following this structure
    /// are <see cref="NumberOfNamedEntries"/> + <see cref="NumberOfIdEntries"/> IMAGE_RESOURCE_DIRECTORY_ENTRY structures.
    /// </summary>
    public class ImageResourceDirectory : IImageResourceDirectory
    {
        public uint Characteristics { get; }
        public uint TimeDateStamp { get; }
        public ushort MajorVersion { get; }
        public ushort MinorVersion { get; }
        public ushort NumberOfNamedEntries { get; }
        public ushort NumberOfIdEntries { get; }

        internal ImageResourceDirectory(PEBinaryReader reader)
        {
            Characteristics = reader.ReadUInt32();
            TimeDateStamp = reader.ReadUInt32();
            MajorVersion = reader.ReadUInt16();
            MinorVersion = reader.ReadUInt16();
            NumberOfNamedEntries = reader.ReadUInt16();
            NumberOfIdEntries = reader.ReadUInt16();
        }
    }
}
