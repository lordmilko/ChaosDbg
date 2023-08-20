namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents the name of an IMAGE_RESOURCE_DIRECTORY_ENTRY.
    /// </summary>
    public interface IImageResourceDirectoryString
    {
        int Length { get; }

        string NameString { get; }
    }

    /// <summary>
    /// Represents the name of an IMAGE_RESOURCE_DIRECTORY_ENTRY.
    /// </summary>
    public class ImageResourceDirectoryString : IImageResourceDirectoryString
    {
        public int Length { get; }

        public string NameString { get; }

        internal ImageResourceDirectoryString(PEBinaryReader reader)
        {
            Length = reader.ReadUInt16();

            NameString = reader.ReadUnicodeString(Length);
        }

        public override string ToString()
        {
            return NameString;
        }
    }
}
