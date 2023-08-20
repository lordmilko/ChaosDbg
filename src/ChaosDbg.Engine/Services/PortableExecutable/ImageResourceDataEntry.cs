namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a data entry pointed to by an IMAGE_RESOURCE_DIRECTORY_ENTRY contained in an IMAGE_RESOURCE_DIRECTORY.
    /// </summary>
    public interface IImageResourceDataEntry
    {
        int OffsetToData { get; }
        uint Size { get; }
        uint CodePage { get; }
        uint Reserved { get; }
    }

    /// <summary>
    /// Represents a data entry pointed to by an IMAGE_RESOURCE_DIRECTORY_ENTRY contained in an IMAGE_RESOURCE_DIRECTORY.
    /// </summary>
    public class ImageResourceDataEntry : IImageResourceDataEntry
    {
        public int OffsetToData { get; }
        public uint Size { get; }
        public uint CodePage { get; }
        public uint Reserved { get; }

        internal ImageResourceDataEntry(PEBinaryReader reader)
        {
            OffsetToData = reader.ReadInt32();
            Size = reader.ReadUInt32();
            CodePage = reader.ReadUInt32();
            Reserved = reader.ReadUInt32();
        }
    }
}
