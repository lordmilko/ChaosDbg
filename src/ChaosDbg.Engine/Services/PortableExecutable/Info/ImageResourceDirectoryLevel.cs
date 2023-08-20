using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Metadata
{
    public interface IImageResourceDirectoryLevel
    {
        IImageResourceDirectory Directory { get; }

        IImageResourceDirectoryEntryInfo[] Entries { get; }

        IImageResourceDirectoryEntryInfo[] TreeEntries { get; }

        IImageResourceDataEntry[] TreeData { get; }

        IImageResourceDirectoryEntryInfo this[string name] { get; }

        IImageResourceDirectoryEntryInfo this[int id] { get; }
    }

    /// <summary>
    /// Represents a tree node encapsulating a given IMAGE_RESOURCE_DIRECTORY and its the IMAGE_RESOURCE_DIRECTORY_ENTRY entries.<para/>
    /// Each IMAGE_RESOURCE_DIRECTORY_ENTRY is itself encapsulated in an <see cref="ImageResourceDirectoryEntryInfo"/>, which may point to
    /// additional <see cref="ImageResourceDirectoryLevel"/> nodes depending on whether each IMAGE_RESOURCE_DIRECTORY_ENTRY points to
    /// another IMAGE_RESOURCE_DIRECTORY or an IMAGE_RESOURCE_DATA_ENTRY.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class ImageResourceDirectoryLevel : IImageResourceDirectoryLevel
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => string.Join(", ", Entries.Select(e => e.ToString()));

        public static ImageResourceDirectoryLevel New(PEFile file)
        {
            int offset;

            if (!file.TryGetDirectoryOffset(file.OptionalHeader.ResourceTableDirectory, out offset, false))
                return null;

            file.Reader.Seek(offset);

            return new ImageResourceDirectoryLevel(file.Reader, offset);
        }

        public IImageResourceDirectory Directory { get; }

        public IImageResourceDirectoryEntryInfo[] Entries { get; }

        public IImageResourceDirectoryEntryInfo[] TreeEntries => GetEntries().ToArray();

        public IImageResourceDataEntry[] TreeData => TreeEntries.Where(e => e.Data != null).Select(e => e.Data).ToArray();

        private IEnumerable<IImageResourceDirectoryEntryInfo> GetEntries()
        {
            if (Entries == null)
                yield break;

            foreach (var entry in Entries)
            {
                yield return entry;

                if (entry.Directory != null)
                {
                    foreach (var childEntry in entry.Directory.TreeEntries)
                        yield return childEntry;
                }
            }
        }

        public IImageResourceDirectoryEntryInfo this[string name]
        {
            get
            {
                foreach (var item in Entries)
                {
                    if (item.Name == null)
                        continue;

                    if (item.Name.NameString == name)
                        return item;
                }

                return null;
            }
        }

        public IImageResourceDirectoryEntryInfo this[int id]
        {
            get
            {
                foreach (var item in Entries)
                {
                    if (item.Name != null)
                        continue;

                    if (item.Entry.Id == id)
                        return item;
                }

                return null;
            }
        }

        public ImageResourceDirectoryLevel(PEBinaryReader reader, int rootOffset)
        {
            Directory = new ImageResourceDirectory(reader);

            var entries = Directory.NumberOfNamedEntries + Directory.NumberOfIdEntries;

            Entries = ReadResourceDirectoryEntries(entries, reader, rootOffset);
        }

        private IImageResourceDirectoryEntryInfo[] ReadResourceDirectoryEntries(int numEntries, PEBinaryReader reader, int rootOffset)
        {
            var list = new List<IImageResourceDirectoryEntryInfo>();

            for (var i = 0; i < numEntries; i++)
                list.Add(new ImageResourceDirectoryEntryInfo(reader, rootOffset));

            return list.ToArray();
        }
    }
}