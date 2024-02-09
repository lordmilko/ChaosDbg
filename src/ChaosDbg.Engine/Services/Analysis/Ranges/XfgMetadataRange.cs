using System.Diagnostics;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("[0x{StartAddress.ToString(\"X\"),nq} - 0x{EndAddress.ToString(\"X\"),nq}] XFG {string.Join(string.Empty, System.Linq.Enumerable.Select(Bytes, b => b.ToString(\"X2\"))),nq}")]
    class XfgMetadataRange : IMetadataRange
    {
        public long StartAddress { get; }
        
        public long EndAddress { get; }

        public byte[] Bytes { get; }

        public long Owner { get; }

        internal XfgMetadataRange(long startAddress, byte[] bytes)
        {
            StartAddress = startAddress;
            EndAddress = startAddress + bytes.Length - 1;
            Bytes = bytes;
            Owner = EndAddress + 1;
        }
    }
}
