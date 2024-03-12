using System;
using System.Diagnostics;
using ChaosDbg.Disasm;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("[0x{StartAddress.ToString(\"X\"),nq} - 0x{EndAddress.ToString(\"X\"),nq}] XFG {Value} -> {Owner.Metadata}")]
    class XfgMetadataRange : IMetadataRange
    {
        public long StartAddress { get; }
        
        public long EndAddress { get; }

        public ulong Value { get; }

        public INativeFunctionChunkRegion Owner { get; }

        internal XfgMetadataRange(XfgMetadataInfo info, INativeFunctionChunkRegion owner)
        {
            StartAddress = info.StartAddress;
            EndAddress = info.EndAddress;
            Value = BitConverter.ToUInt64(info.Bytes, 0);
            Owner = owner;
        }

        public bool IsEquivalent(ulong other)
        {
            other |= 1;

            //Most of the checks in ntdll!LdrpDispatchUserCallTargetXFG apply to the address of the function, not the XFG hash itself

            return Value == other;
        }
    }
}
