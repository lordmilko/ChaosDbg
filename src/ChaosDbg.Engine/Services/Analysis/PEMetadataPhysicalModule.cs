using System;
using System.IO;
using System.Runtime.CompilerServices;
using ChaosDbg.Disasm;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ClrDebug;

namespace ChaosDbg.Analysis
{
    public class PEMetadataPhysicalModule : PEMetadataModule
    {
#pragma warning disable RS0030 //It's physical so it's OK
        public override CORDB_ADDRESS Address => PhysicalPEFile.OptionalHeader.ImageBase;
#pragma warning restore RS0030

        /// <summary>
        /// Gets the original on-disk base address of the module.
        /// </summary>
#pragma warning disable RS0030 //It's physical so it's OK
        public CORDB_ADDRESS PhysicalAddress => (long) PhysicalPEFile.OptionalHeader.ImageBase;
#pragma warning restore RS0030

        /// <summary>
        /// Gets basic information about the PE File as it exists on disk.<para/>
        /// This PE File does not contain optional data directories. To retrieve this information,
        /// use <see cref="PEMetadataVirtualModule.VirtualPEFile"/>.
        /// </summary>
        public PEFile PhysicalPEFile { get; }

        internal PEMetadataPhysicalModule(
            string fileName,
            IUnmanagedSymbolModule symbolModule,
            PEFile physicalPEFile,
            Func<Stream, INativeDisassembler> createDisassembler) : base(fileName, symbolModule, createDisassembler)
        {
            PhysicalPEFile = physicalPEFile;
        }

        public override bool ContainsAddress(long address) =>
            ContainsPhysicalAddress(address);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsPhysicalAddress(long address)
        {
#pragma warning disable RS0030 //We're physical so it's OK
            var start = PhysicalPEFile.OptionalHeader.ImageBase;
#pragma warning restore RS0030
            var end = start + PhysicalPEFile.OptionalHeader.SizeOfImage;

            if (address >= start && address <= end)
                return true;

            return false;
        }

        public virtual IMetadataRange FindByPhysicalAddress(long address) => FindByAddress(address);
    }
}
