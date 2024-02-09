using System;
using System.IO;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.Tests
{
    class MockRelayPEFile : IPEFile
    {
        public bool IsLoadedImage => peFile.IsLoadedImage;
        public IImageFileHeader FileHeader => peFile.FileHeader;
        public IImageOptionalHeader OptionalHeader => peFile.OptionalHeader;
        public IImageSectionHeader[] SectionHeaders => peFile.SectionHeaders;

        private IImageExportDirectoryInfo exportDirectory;
        private bool exportDirectorySet;
        public IImageExportDirectoryInfo ExportDirectory
        {
            get => exportDirectorySet ? exportDirectory : peFile.ExportDirectory;
            set
            {
                exportDirectorySet = true;
                exportDirectory = value;
            }
        }

        public IImageImportDescriptorInfo[] ImportDirectory => peFile.ImportDirectory;
        public IImageResourceDirectoryInfo ResourceDirectory => peFile.ResourceDirectory;

        private IImageRuntimeFunctionInfo[] exceptionDirectory;

        public IImageRuntimeFunctionInfo[] ExceptionDirectory
        {
            get => exceptionDirectory ?? peFile.ExceptionDirectory;
            set => exceptionDirectory = value;
        }

        public IImageDebugDirectoryInfo DebugDirectoryInfo => peFile.DebugDirectoryInfo;
        public IImageLoadConfigDirectory LoadConfigDirectory => peFile.LoadConfigDirectory;
        public IImageBoundImportDescriptorInfo[] BoundImportTableDirectory => peFile.BoundImportTableDirectory;
        public IImageThunkDataInfo[] ImportAddressTableDirectory => peFile.ImportAddressTableDirectory;
        public IImageDelayLoadDescriptorInfo[] DelayImportTableDirectory => peFile.DelayImportTableDirectory;
        public IImageCor20Header Cor20Header => peFile.Cor20Header;

        private IPEFile peFile;

        public MockRelayPEFile(IPEFile peFile)
        {
            this.peFile = peFile;
        }

        public void ReadDataDirectories(Stream stream, PEFileDirectoryFlags flags, IPESymbolResolver symbolResolver = null)
        {
            throw new NotImplementedException();
        }

        public bool TryGetDirectoryOffset(IImageDataDirectory entry, out int offset, bool canCrossSectionBoundary)
        {
            throw new NotImplementedException();
        }

        public bool TryGetOffset(int rva, out int offset)
        {
            offset = 0;
            return true;
        }

        public int GetSectionContainingRVA(int rva) => peFile.GetSectionContainingRVA(rva);
    }
}
