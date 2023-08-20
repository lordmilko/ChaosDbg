namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents the IMAGE_COR20_HEADER type that is pointed to by IMAGE_OPTIONAL_HEADER.DataDirectory[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR] in managed assemblies.
    /// </summary>
    public interface IImageCor20Header
    {
        ushort MajorRuntimeVersion { get; }
        ushort MinorRuntimeVersion { get; }
        IImageDataDirectory Metadata { get; }
        COMIMAGE_FLAGS Flags { get; }
        int EntryPointTokenOrRelativeVirtualAddress { get; }
        IImageDataDirectory Resources { get; }
        IImageDataDirectory StrongNameSignature { get; }
        IImageDataDirectory CodeManagerTable { get; }
        IImageDataDirectory VtableFixups { get; }
        IImageDataDirectory ExportAddressTableJumps { get; }
        IImageDataDirectory ManagedNativeHeader { get; }
    }

    /// <summary>
    /// Represents the IMAGE_COR20_HEADER type that is pointed to by IMAGE_OPTIONAL_HEADER.DataDirectory[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR] in managed assemblies.
    /// </summary>
    public class ImageCor20Header : IImageCor20Header
    {
        public ushort MajorRuntimeVersion { get; }
        public ushort MinorRuntimeVersion { get; }
        public IImageDataDirectory Metadata { get; }
        public COMIMAGE_FLAGS Flags { get; }
        public int EntryPointTokenOrRelativeVirtualAddress { get; }
        public IImageDataDirectory Resources { get; }
        public IImageDataDirectory StrongNameSignature { get; }
        public IImageDataDirectory CodeManagerTable { get; }
        public IImageDataDirectory VtableFixups { get; }
        public IImageDataDirectory ExportAddressTableJumps { get; }
        public IImageDataDirectory ManagedNativeHeader { get; }

        internal ImageCor20Header(PEBinaryReader reader)
        {
            reader.ReadInt32(); //Skip byte count

            MajorRuntimeVersion = reader.ReadUInt16();
            MinorRuntimeVersion = reader.ReadUInt16();
            Metadata = new ImageDataDirectory(reader);
            Flags = (COMIMAGE_FLAGS) reader.ReadUInt32();
            EntryPointTokenOrRelativeVirtualAddress = reader.ReadInt32();
            Resources = new ImageDataDirectory(reader);
            StrongNameSignature = new ImageDataDirectory(reader);
            CodeManagerTable = new ImageDataDirectory(reader);
            VtableFixups = new ImageDataDirectory(reader);
            ExportAddressTableJumps = new ImageDataDirectory(reader);
            ManagedNativeHeader = new ImageDataDirectory(reader);
        }
    }
}
