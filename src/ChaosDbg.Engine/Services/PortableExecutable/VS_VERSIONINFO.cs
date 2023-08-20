namespace ChaosDbg.Metadata
{
    public interface IVS_VERSIONINFO
    {
        ushort Length { get; }
        ushort ValueLength { get; }
        ushort Type { get; }
        string Key { get; }
        ushort Padding1 { get; }
        VS_FIXEDFILEINFO Value { get; }
    }

    public class VS_VERSIONINFO : IVS_VERSIONINFO
    {
        public ushort Length { get; }
        public ushort ValueLength { get; }
        public ushort Type { get; }
        public string Key { get; }
        public ushort Padding1 { get; }
        public VS_FIXEDFILEINFO Value { get; }

        //VS_VERSIONINFO is a variable length structure. Only the core fields are defined
        //Don't know how to handle Padding2 and Children if ValueLength is 0, since then
        //we won't have Value either.
        //public ushort Padding2;
        //public ushort Children;

        internal VS_VERSIONINFO(PEBinaryReader reader)
        {
            Length = reader.ReadUInt16();
            ValueLength = reader.ReadUInt16();

            Type = reader.ReadUInt16();
            Key = reader.ReadUnicodeString(16); //VS_VERSION_INFO + \0
            Padding1 = reader.ReadUInt16();
            Value = new VS_FIXEDFILEINFO(reader);
        }
    }
}
