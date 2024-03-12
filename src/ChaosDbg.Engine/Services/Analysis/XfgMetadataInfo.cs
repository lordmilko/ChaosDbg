namespace ChaosDbg.Analysis
{
    struct XfgMetadataInfo
    {
        public long StartAddress { get; }

        public long EndAddress { get; }

        public byte[] Bytes { get; }

        public long Owner { get; }

        public XfgMetadataInfo(long startAddress, byte[] bytes)
        {
            StartAddress = startAddress;
            EndAddress = startAddress + bytes.Length - 1;
            Bytes = bytes;
            Owner = EndAddress + 1;
        }
    }
}
