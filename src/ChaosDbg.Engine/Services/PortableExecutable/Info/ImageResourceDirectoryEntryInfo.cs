namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Encapsulates an IMAGE_RESOURCE_DIRECTORY entry that has been read from an IMAGE_RESOURCE_DIRECTORY,
    /// as well as the name (IMAGE_RESOURCE_DIRECTORY_STRING), data (IMAGE_RESOURCE_DATA_ENTRY) and directory hierarchy (IMAGE_RESOURCE_DIRECTORY) it may point to.
    /// </summary>
    public interface IImageResourceDirectoryEntryInfo
    {
        /// <summary>
        /// Gets the name of the IMAGE_RESOURCE_DIRECTORY_ENTRY. Only applies if <see cref="ImageResourceDirectoryEntry.IsName"/> is true.
        /// </summary>
        IImageResourceDirectoryString Name { get; }

        /// <summary>
        /// Gets the IMAGE_RESOURCE_DIRECTORY_ENTRY that was read from the IMAGE_RESOURCE_DIRECTORY.
        /// </summary>
        IImageResourceDirectoryEntry Entry { get; }

        /// <summary>
        /// Gets the descending hierarchy of the IMAGE_RESOURCE_DIRECTORY that the IMAGE_RESOURCE_DIRECTORY_ENTRY points to.<para/>
        /// Only applies if <see cref="ImageResourceDirectoryEntry.IsDirectory"/> is true.
        /// </summary>
        IImageResourceDirectoryLevel Directory { get; }

        /// <summary>
        /// Gets the data that the IMAGE_RESOURCE_DIRECTORY points to. Only applies if <see cref="ImageResourceDirectoryEntry.IsDirectory"/> is false.
        /// </summary>
        IImageResourceDataEntry Data { get; }

        IImageResourceDirectoryEntryInfo this[string name] { get; }

        IImageResourceDirectoryEntryInfo this[int id] { get; }
    }

    /// <summary>
    /// Encapsulates an IMAGE_RESOURCE_DIRECTORY entry that has been read from an IMAGE_RESOURCE_DIRECTORY,
    /// as well as the name (IMAGE_RESOURCE_DIRECTORY_STRING), data (IMAGE_RESOURCE_DATA_ENTRY) and directory hierarchy (IMAGE_RESOURCE_DIRECTORY) it may point to.
    /// </summary>
    public class ImageResourceDirectoryEntryInfo : IImageResourceDirectoryEntryInfo
    {
        /// <inheritdoc />
        public IImageResourceDirectoryString Name { get; }

        /// <inheritdoc />
        public IImageResourceDirectoryEntry Entry { get; }

        /// <inheritdoc />
        public IImageResourceDirectoryLevel Directory { get; }

        /// <inheritdoc />
        public IImageResourceDataEntry Data { get; }

        public ImageResourceDirectoryEntryInfo(PEBinaryReader reader, int rootOffset)
        {
            Entry = new ImageResourceDirectoryEntry(reader);

            //Store the old position so we can revert back to it after reading all the items this entry points to.
            //We need to revert back to the old position so that the sibling IMAGE_RESOURCE_DIRECTORY_ENTRY items of this one that immediately
            //follow in memory may also be read
            var oldPosition = (int)reader.Position;

            if (Entry.IsName)
            {
                reader.Seek(rootOffset + Entry.NameOffset);

                Name = new ImageResourceDirectoryString(reader);
            }

            if (Entry.IsDirectory)
            {
                //The offset given is relative to the root offset; i.e. the offset of the IMAGE_RESOURCE_DIRECTORY that was pointed to by the IMAGE_OPTIONAL_HEADER
                reader.Seek(rootOffset + Entry.OffsetToDirectory);

                Directory = new ImageResourceDirectoryLevel(reader, rootOffset);
            }
            else
            {
                reader.Seek(rootOffset + Entry.OffsetToData);

                Data = new ImageResourceDataEntry(reader);
            }

            reader.Seek(oldPosition);
        }

        public IImageResourceDirectoryEntryInfo this[string name] => Directory?[name];

        public IImageResourceDirectoryEntryInfo this[int id] => Directory?[id];

        public override string ToString()
        {
            if (Entry.IsName)
                return Name.ToString();

            return Entry.Id.ToString();
        }
    }
}