using System;
using System.Collections.Generic;
using System.Diagnostics;
using SymHelp.Symbols.MicrosoftPdb;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("[0x{StartAddress.ToString(\"X\"),nq} - 0x{EndAddress.ToString(\"X\"),nq}] Data {Metadata}")]
    class DataMetadataRange : IMetadataRange
    {
        public long StartAddress => Metadata.Address;

        public long EndAddress { get; }

        public InstructionDiscoverySource Metadata { get; }

        public List<DataMetadataRange> Children { get; private set; }

        internal DataMetadataRange(InstructionDiscoverySource metadata, bool is32Bit)
        {
            Metadata = metadata;

            long length;

            if (metadata.FoundBy.HasFlag(FoundBy.ExternalJmp))
                length = is32Bit ? 4 : 8;
            else if (metadata.FoundBy.HasFlag(FoundBy.Symbol))
            {
                if (metadata.Symbol is MicrosoftPdbSymbol m)
                    length = m.SafeDiaSymbol.Length;
                else
                    throw new NotImplementedException();
            }
            else
                length = 1;

            //ntdll!__castguard_check_failure_os_handled_fptr has a length of 0, but IDA has it using 1 byte
            if (length == 0)
                length = 1;

            EndAddress = metadata.Address + length - 1;
        }

        public void AddChild(DataMetadataRange child)
        {
            if (Children == null)
                Children = new List<DataMetadataRange>();

            Children.Add(child);
        }
    }
}
