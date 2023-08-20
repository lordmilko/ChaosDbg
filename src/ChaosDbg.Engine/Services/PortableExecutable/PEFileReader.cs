using System.IO;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Provides facilities for reading Portable Executable (PE) files.
    /// </summary>
    interface IPEFileReader
    {
        /// <summary>
        /// Reads a PE file from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the PE file from.</param>
        /// <param name="isLoadedImage">Whether the stream represents a PE file that is in memory or on disk.</param>
        /// <returns>A PE file.</returns>
        IPEFile ReadStream(Stream stream, bool isLoadedImage);
    }

    /// <summary>
    /// Provides facilities for reading Portable Executable (PE) files.
    /// </summary>
    public class PEFileReader : IPEFileReader
    {
        /// <inheritdoc />
        public IPEFile ReadStream(Stream stream, bool isLoadedImage)
        {
            return new PEFile(stream, isLoadedImage);
        }
    }
}
