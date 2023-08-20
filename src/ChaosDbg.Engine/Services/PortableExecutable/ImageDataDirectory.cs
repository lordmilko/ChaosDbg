using System.Diagnostics;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Describes the location and size of a data directory that may exist in the image.<para/>
    /// Not to be confused with <see cref="ImageDebugDirectory"/>, which represents an entry within
    /// _the_ debug directory region that may be pointed to by a given <see cref="ImageDataDirectory"/>.
    /// </summary>
    public interface IImageDataDirectory
    {
        int RelativeVirtualAddress { get; }
        int Size { get; }
    }

    /// <summary>
    /// Describes the location and size of a data directory that may exist in the image.<para/>
    /// Not to be confused with <see cref="ImageDebugDirectory"/>, which represents an entry within
    /// _the_ debug directory region that may be pointed to by a given <see cref="ImageDataDirectory"/>.
    /// </summary>
    [DebuggerDisplay("RVA = {RelativeVirtualAddress}, Size = {Size}")]
    public struct ImageDataDirectory : IImageDataDirectory
    {
        public int RelativeVirtualAddress { get; }
        public int Size { get; }

        internal ImageDataDirectory(PEBinaryReader reader)
        {
            RelativeVirtualAddress = reader.ReadInt32();
            Size = reader.ReadInt32();
        }
    }
}
