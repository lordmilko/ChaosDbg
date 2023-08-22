using System;
using System.Collections.Generic;
using System.IO;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a Portable Executable (PE) file.
    /// </summary>
    public interface IPEFile
    {
        /// <summary>
        /// Gets whether the image exists within the memory a live process, or exists on disk. Offsets are slightly different in some areas when in memory vs on disk.
        /// </summary>
        bool IsLoadedImage { get; }

        /// <summary>
        /// Gets the file header of the image. This value represents the IMAGE_NT_HEADERS.FileHeader field.
        /// </summary>
        IImageFileHeader FileHeader { get; }

        /// <summary>
        /// Gets the optional header of the image. This value represents the IMAGE_NT_HEADERS.OptionalHeader field.
        /// </summary>
        IImageOptionalHeader OptionalHeader { get; }

        /// <summary>
        /// Gets the section headers of the image. These values represent the IMAGE_SECTION_HEADER values (e.g. .text, .data) that immediately follow the <see cref="OptionalHeader"/>.<para/>
        /// Each section header points to a relative location within the image at which that section actually resides.
        /// </summary>
        IImageSectionHeader[] SectionHeaders { get; }

        #region Directories

        //Items pointed to by directory entries in ImageOptionalHeader

        /// <summary>
        /// Gets the information about the export table in the image, and where the components of it can be found.<para/>
        /// This value is pointed to by <see cref="ImageOptionalHeader.ExportTableDirectory"/>.
        /// </summary>
        IImageExportDirectory ExportDirectory { get; }

        IImageCor20Header Cor20Header { get; }

        IImageDebugDirectoryInfo DebugDirectoryInfo { get; }

        IImageResourceDirectoryInfo ResourceDirectoryInfo { get; }

        #endregion

        /// <summary>
        /// Tries to get the physical offset within the image of the full directory pointed to by an <see cref="ImageDataDirectory"/>.
        /// </summary>
        /// <param name="entry">The <see cref="ImageDataDirectory"/> that may point to a full directory within the image.</param>
        /// <param name="offset">Offset from the start of the image to the given directory data.</param>
        /// <param name="canCrossSectionBoundary">Whether size of the entry is allowed to cross over the end of the section boundary.</param>
        /// <returns>True if the <see cref="ImageDataDirectory"/> points to a valid section, otherwise false.</returns>
        bool TryGetDirectoryOffset(IImageDataDirectory entry, out int offset, bool canCrossSectionBoundary);

        /// <summary>
        /// Tries to get the physical offset within the image of a specified relative virtual address.
        /// </summary>
        /// <param name="rva">The relative virtual address within the image to translate.</param>
        /// <param name="offset">The translated physical address.</param>
        /// <returns>True if the RVA was translated to a physical offset, otherwise false.</returns>
        bool TryGetOffset(int rva, out int offset);

        /// <summary>
        /// Gets the section that contains the specified Relative Virtual Address.
        /// </summary>
        /// <param name="rva">The RVA whose containing section should be found.</param>
        /// <returns>The index of section that contains the RVA, or -1 if none was found.</returns>
        int GetSectionContainingRVA(int rva);
    }

    /// <summary>
    /// Represents a Portable Executable (PE) file.
    /// </summary>
    public class PEFile : IPEFile
    {
        internal const ushort DosSignature = 0x5A4D;     //MZ
        internal const int PESignatureOffsetLocation = 0x3C;
        internal const uint PESignature = 0x00004550;    //PE00

        /// <inheritdoc />
        public bool IsLoadedImage { get; }

        /// <inheritdoc />
        public IImageFileHeader FileHeader { get; }

        /// <inheritdoc />
        public IImageOptionalHeader OptionalHeader { get; }

        /// <inheritdoc />
        public IImageSectionHeader[] SectionHeaders { get; }

        #region Directories

        //Items pointed to by directory entries in ImageOptionalHeader

        /// <inheritdoc />
        public IImageExportDirectory ExportDirectory { get; }

        public IImageCor20Header Cor20Header { get; }

        public IImageDebugDirectoryInfo DebugDirectoryInfo { get; }

        public IImageResourceDirectoryInfo ResourceDirectoryInfo { get; }

        #endregion

        public PEFile(Stream stream, bool isLoadedImage)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead || !stream.CanSeek)
                throw new InvalidOperationException("Stream must support read and seek operations.");

            IsLoadedImage = isLoadedImage;

            var reader = new PEBinaryReader(stream);

            //The PE file format extends the COFF format; if the file doesn't start with MZ,
            //it may be an old school COFF file (i.e. *.obj)
            var isCoffOnly = SkipDosHeader(reader);

            FileHeader = new ImageFileHeader(reader);

            if (!isCoffOnly)
                OptionalHeader = new ImageOptionalHeader(reader);

            SectionHeaders = ReadSectionHeaders(reader);

            //Try read various directories. Some entries are optional, while others are mandatory

            if (!isCoffOnly)
                Cor20Header = ReadCor20Header(reader);

            ExportDirectory = ReadExportDirectory(reader);
            DebugDirectoryInfo = new ImageDebugDirectoryInfo(this, reader);
            ResourceDirectoryInfo = new ImageResourceDirectoryInfo(this, reader);
        }

        private IImageSectionHeader[] ReadSectionHeaders(PEBinaryReader reader)
        {
            if (FileHeader.NumberOfSections < 0)
                throw new BadImageFormatException("Invalid number of sections declared in PE header.");

            var list = new List<IImageSectionHeader>();

            for (var i = 0; i < FileHeader.NumberOfSections; i++)
                list.Add(new ImageSectionHeader(reader));

            return list.ToArray();
        }

        private ImageCor20Header ReadCor20Header(PEBinaryReader reader)
        {
            int offset;
            if (TryCalculateCor20HeaderOffset(out offset))
            {
                reader.Seek(offset);
                return new ImageCor20Header(reader);
            }

            return null;
        }

        private ImageExportDirectory ReadExportDirectory(PEBinaryReader reader)
        {
            int offset;

            if (!TryGetDirectoryOffset(OptionalHeader.ExportTableDirectory, out offset, true))
                return null;

            reader.Seek(offset);
            return new ImageExportDirectory(reader);
        }

        #region Helpers

        private bool SkipDosHeader(PEBinaryReader reader)
        {
            var dosSig = reader.ReadUInt16();

            if (dosSig != DosSignature)
            {
                if (dosSig != 0 || reader.ReadUInt16() != 0xffff)
                {
                    reader.Seek(0);
                    return true;
                }

                throw new BadImageFormatException("Unknown file format.");
            }

            reader.Seek(PESignatureOffsetLocation);

            int ntHeaderOffset = reader.ReadInt32();
            reader.Seek(ntHeaderOffset);

            var ntSignature = reader.ReadUInt32();

            if (ntSignature != PESignature)
                throw new BadImageFormatException("Invalid PE signature.");

            return false;
        }

        private bool TryCalculateCor20HeaderOffset(out int startOffset)
        {
            if (!TryGetDirectoryOffset(OptionalHeader.CorHeaderTableDirectory, out startOffset, false))
            {
                startOffset = -1;
                return false;
            }

            var length = OptionalHeader.CorHeaderTableDirectory.Size;

            const int sizeOfCorHeader = 72;

            if (length < sizeOfCorHeader)
                throw new BadImageFormatException("Invalid COR header size.");

            return true;
        }

        /// <inheritdoc />
        public bool TryGetDirectoryOffset(IImageDataDirectory entry, out int offset, bool canCrossSectionBoundary)
        {
            var sectionIndex = GetSectionContainingRVA(entry.RelativeVirtualAddress);

            if (sectionIndex < 0)
            {
                offset = -1;
                return false;
            }

            var section = SectionHeaders[sectionIndex];
            var relativeOffset = entry.RelativeVirtualAddress - section.VirtualAddress;

            if (!canCrossSectionBoundary && entry.Size > section.VirtualSize - relativeOffset)
                throw new BadImageFormatException("Section too small.");

            offset = IsLoadedImage
                ? entry.RelativeVirtualAddress
                : section.PointerToRawData + relativeOffset;

            return true;
        }

        public bool TryGetOffset(int rva, out int offset)
        {
            var sectionIndex = GetSectionContainingRVA(rva);

            if (sectionIndex < 0)
            {
                offset = -1;
                return false;
            }

            var section = SectionHeaders[sectionIndex];
            var relativeOffset = rva - section.VirtualAddress;

            offset = IsLoadedImage
                ? rva
                : section.PointerToRawData + relativeOffset;

            return true;
        }

        /// <inheritdoc />
        public int GetSectionContainingRVA(int rva)
        {
            for (var i = 0; i < SectionHeaders.Length; i++)
            {
                var start = SectionHeaders[i].VirtualAddress;
                var end = SectionHeaders[i].VirtualAddress + SectionHeaders[i].VirtualSize;

                if (start <= rva && rva < end)
                    return i;
            }

            return -1;
        }

        internal int GetOffset(IImageDebugDirectory entry)
        {
            int dataOffset = IsLoadedImage
                ? entry.AddressOfRawData
                : entry.PointerToRawData;

            return dataOffset;
        }

        #endregion
    }
}
