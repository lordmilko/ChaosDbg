using ChaosDbg.Metadata;

namespace ChaosDbg.Tests
{
    class MockImageOptionalHeader : IImageOptionalHeader
    {
        public PEMagic Magic { get; }
        public byte MajorLinkerVersion { get; }
        public byte MinorLinkerVersion { get; }
        public int SizeOfCode { get; }
        public int SizeOfInitializedData { get; }
        public int SizeOfUninitializedData { get; }
        public int AddressOfEntryPoint { get; }
        public int BaseOfCode { get; }
        public int BaseOfData { get; }
        public ulong ImageBase { get; set; }
        public int SectionAlignment { get; }
        public int FileAlignment { get; }
        public ushort MajorOperatingSystemVersion { get; }
        public ushort MinorOperatingSystemVersion { get; }
        public ushort MajorImageVersion { get; }
        public ushort MinorImageVersion { get; }
        public ushort MajorSubsystemVersion { get; }
        public ushort MinorSubsystemVersion { get; }
        public int SizeOfImage { get; }
        public int SizeOfHeaders { get; }
        public uint CheckSum { get; }
        public ImageSubsystem Subsystem { get; }
        public ImageDllCharacteristics DllCharacteristics { get; }
        public ulong SizeOfStackReserve { get; }
        public ulong SizeOfStackCommit { get; }
        public ulong SizeOfHeapReserve { get; }
        public ulong SizeOfHeapCommit { get; }
        public int NumberOfRvaAndSizes { get; }
        public IImageDataDirectory ExportTableDirectory { get; }
        public IImageDataDirectory ImportTableDirectory { get; }
        public IImageDataDirectory ResourceTableDirectory { get; }
        public IImageDataDirectory ExceptionTableDirectory { get; }
        public IImageDataDirectory SecurityTableDirectory { get; }
        public IImageDataDirectory BaseRelocationTableDirectory { get; }
        public IImageDataDirectory DebugTableDirectory { get; }
        public IImageDataDirectory CopyrightTableDirectory { get; }
        public IImageDataDirectory GlobalPointerTableDirectory { get; }
        public IImageDataDirectory ThreadLocalStorageTableDirectory { get; }
        public IImageDataDirectory LoadConfigTableDirectory { get; }
        public IImageDataDirectory BoundImportTableDirectory { get; }
        public IImageDataDirectory ImportAddressTableDirectory { get; }
        public IImageDataDirectory DelayImportTableDirectory { get; }
        public IImageDataDirectory CorHeaderTableDirectory { get; }
    }
}
