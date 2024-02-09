using System.Diagnostics;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("[0x{StartAddress.ToString(\"X\"),nq} - 0x{EndAddress.ToString(\"X\"),nq}] Header")]
    class PEHeaderMetadataRange : IMetadataRange
    {
        public long StartAddress { get; }
        public long EndAddress { get; }

        internal PEHeaderMetadataRange(long startAddress, long endAddress)
        {
            StartAddress = startAddress;
            EndAddress = endAddress;
        }
    }
}
