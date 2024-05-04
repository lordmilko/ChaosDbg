using System.Text;
using ChaosDbg.Disasm;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ClrDebug;

namespace ChaosDbg.Analysis
{
    #nullable enable

    class InstructionDiscoverySource : INativeFunctionMetadata
    {
        public long Address { get; }

#if DEBUG
        public CORDB_ADDRESS PhysicalAddress { get; set; }
#endif

        public FoundBy FoundBy { get; set; }

        public FoundBySubType FoundBySubType { get; set; }

        public InstructionDiscoveryPriority Priority => new InstructionDiscoveryPriority(FoundBy);

        public DiscoveryTrustLevel TrustLevel { get; set; }

        public InstructionDiscoveryResult Result { get; set; }

        public bool? DoesReturn { get; set; }

        public ImageSectionHeader? Section { get; set; }

#if DEBUG
        //Only store the discovered code on the discovery source in debug so we can investigate what instructions a given source yielded

        public NativeCodeRegionCollection? DiscoveredCode { get; set; }
#endif

        #region Sources

        public IUnmanagedSymbol? Symbol { get; set; }

        public ImageRuntimeFunctionInfo? RuntimeFunction { get; set; }

        public IImageExportInfo? Export { get; set; }

        public ByteSequence? ByteSequence { get; set; }

        public InstructionDiscoverySource? Caller { get; set; }

        #endregion

        public InstructionDiscoverySource(long address, FoundBy foundBy, DiscoveryTrustLevel trustLevel)
        {
            Address = address;
            FoundBy = foundBy;
            TrustLevel = trustLevel;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

#if DEBUG
            CORDB_ADDRESS addr = PhysicalAddress == 0 ? Address : PhysicalAddress;
#else
            CORDB_ADDRESS addr = Address;
#endif

            if (Symbol != null)
                builder.Append("[").Append(Symbol.Name).Append("] ");
            else if (Export != null)
                builder.Append("[").Append(Export.Name).Append("] ");

            builder.Append($"{addr} ({FoundBy})");

            return builder.ToString();
        }
    }
}
