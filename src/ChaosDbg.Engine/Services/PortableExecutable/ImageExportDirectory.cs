namespace ChaosDbg.Metadata
{
    public interface IImageExportDirectory
    {
        int Characteristics { get; }
        int TimeDateStamp { get; }
        ushort MajorVersion { get; }
        ushort MinorVersion { get; }
        int Name { get; }
        int Base { get; }
        int NumberOfFunctions { get; }
        int NumberOfNames { get; }
        int AddressOfFunctions { get; }
        int AddressOfNames { get; }
        int AddressOfNameOrdinals { get; }
    }

    /// <summary>
    /// Represents the IMAGE_EXPORT_DIRECTORY structure, that describes the locations and components of the export table within the image.
    /// </summary>
    public class ImageExportDirectory : IImageExportDirectory
    {
        public int Characteristics { get; }
        public int TimeDateStamp { get; }
        public ushort MajorVersion { get; }
        public ushort MinorVersion { get; }
        public int Name { get; }
        public int Base { get; }
        public int NumberOfFunctions { get; }
        public int NumberOfNames { get; }
        public int AddressOfFunctions { get; }
        public int AddressOfNames { get; }
        public int AddressOfNameOrdinals { get; }

        public static int Size =
            sizeof(int) + //Characteristics
            sizeof(int) + //TimeDateStamp
            sizeof(ushort) + //MajorVersion
            sizeof(ushort) + //MinorVersion
            sizeof(int) + //Name
            sizeof(int) + //Base
            sizeof(int) + //NumberOfFunctions
            sizeof(int) + //NumberOfNames
            sizeof(int) + //AddressOfFunctions
            sizeof(int) + //AddressOfNames
            sizeof(int); //AddressOfNameOrdinals

        internal ImageExportDirectory(PEBinaryReader reader)
        {
            Characteristics = reader.ReadInt32();
            TimeDateStamp = reader.ReadInt32();
            MajorVersion = reader.ReadUInt16();
            MinorVersion = reader.ReadUInt16();
            Name = reader.ReadInt32();
            Base = reader.ReadInt32();
            NumberOfFunctions = reader.ReadInt32();
            NumberOfNames = reader.ReadInt32();
            AddressOfFunctions = reader.ReadInt32();
            AddressOfNames = reader.ReadInt32();
            AddressOfNameOrdinals = reader.ReadInt32();
        }
    }
}
