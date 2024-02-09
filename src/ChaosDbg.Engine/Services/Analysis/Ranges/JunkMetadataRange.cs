using System.Diagnostics;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("[0x{StartAddress.ToString(\"X\"),nq} - 0x{EndAddress.ToString(\"X\"),nq}] Junk")]
    class JunkMetadataRange : IMetadataRange
    {
        public long StartAddress { get; }

        public long EndAddress { get; }

        public byte[] Bytes { get; internal set; }

        internal JunkMetadataRange(long startAddress, long endAddress, byte[] bytes)
        {
            StartAddress = startAddress;
            EndAddress = endAddress;
            Bytes = bytes;
        }
    }
}
