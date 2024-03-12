using System;
using System.IO;
using System.Runtime.CompilerServices;
using ChaosDbg.Disasm;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ClrDebug;

namespace ChaosDbg.Analysis
{
    /// <summary>
    /// Provides advanced information regarding the contents of a Portable Executable assembly file.
    /// </summary>
    public class PEMetadataVirtualModule : PEMetadataPhysicalModule
    {
        /// <summary>
        /// Gets the in-memory base address of the module.
        /// </summary>
        public override CORDB_ADDRESS Address { get; }

        /// <summary>
        /// Gets information about the PE File as it exists in memory.
        /// </summary>
        public IPEFile VirtualPEFile { get; }

        internal PEMetadataVirtualModule(
            string fileName,
            CORDB_ADDRESS address,
            IUnmanagedSymbolModule symbolModule,
            IPEFile physicalPEFile,
            IPEFile virtualPEFile,
            Func<Stream, INativeDisassembler> createDisassembler) : base(fileName, symbolModule, physicalPEFile, createDisassembler)
        {
            Address = address;
            VirtualPEFile = virtualPEFile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public
#if DEBUG
        override
#endif
        long GetPhysicalAddress(long virtualAddress) => (virtualAddress - Address) + PhysicalPEFile.OptionalHeader.ImageBase;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetVirtualAddress(long physicalAddress) => (physicalAddress - (long) PhysicalPEFile.OptionalHeader.ImageBase) + Address;

        public override bool ContainsAddress(long address) =>
            ContainsVirtualAddress(address);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsVirtualAddress(long address)
        {
            //Do not use VirtualPEFile.OptionalHeader.ImageBase. It doesn't match the actual loaded
            //base address in managed modules
            var start = (long) Address;
            var end = start + VirtualPEFile.OptionalHeader.SizeOfImage;

            if (address >= start && address <= end)
                return true;

            return false;
        }

        public override IMetadataRange FindByPhysicalAddress(long address) => FindByAddress(GetVirtualAddress(address));

        public IMetadataRange FindByVirtualAddress(long address) => FindByAddress(address);

        public override string ToString()
        {
            return FileName;
        }
    }
}
