using System.IO;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Provides facilities for reading Portable Executable (PE) files.
    /// </summary>
    public interface IPEFileProvider
    {
        /// <summary>
        /// Reads a PE file from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the PE file from.<para/>
        /// All information is immediately read from the stream, and the stream is not stored.</param>
        /// <param name="isLoadedImage">Whether the stream represents a PE file that is in memory or on disk.</param>
        /// <returns>A PE file.</returns>
        IPEFile ReadStream(Stream stream, bool isLoadedImage);

        /// <summary>
        /// Reads a PE file from a file path.
        /// </summary>
        /// <param name="path">The path to the PE file to read.</param>
        /// <returns>A PE file.</returns>
        IPEFile ReadFile(string path);
    }

    /// <summary>
    /// Provides facilities for reading Portable Executable (PE) files.
    /// </summary>
    public class PEFileProvider : IPEFileProvider
    {
        /// <inheritdoc />
        public IPEFile ReadStream(Stream stream, bool isLoadedImage)
        {
            return new PEFile(stream, isLoadedImage);
        }

        /// <inheritdoc />
        public IPEFile ReadFile(string path)
        {
            using (var fs = File.OpenRead(path))
                return new PEFile(fs, false);
        }
    }
}
