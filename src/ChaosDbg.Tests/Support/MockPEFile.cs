using System;
using ChaosLib.Metadata;

namespace ChaosDbg.Tests
{
    class MockPEFile : IPEFile
    {
        public bool IsLoadedImage { get; }
        public IImageFileHeader FileHeader { get; }
        public IImageOptionalHeader OptionalHeader { get; set; }
        public IImageSectionHeader[] SectionHeaders { get; }
        public IImageExportDirectory ExportDirectory { get; }
        public IImageImportDescriptorInfo[] ImportDirectory { get; }
        public IImageCor20Header Cor20Header { get; }
        public IImageDebugDirectoryInfo DebugDirectoryInfo { get; }
        public IImageResourceDirectoryInfo ResourceDirectoryInfo { get; }
        public bool TryGetDirectoryOffset(IImageDataDirectory entry, out int offset, bool canCrossSectionBoundary)
        {
            throw new NotImplementedException();
        }

        public bool TryGetOffset(int rva, out int offset)
        {
            offset = 0;
            return true;
        }

        public int GetSectionContainingRVA(int rva)
        {
            throw new NotImplementedException();
        }
    }
}
