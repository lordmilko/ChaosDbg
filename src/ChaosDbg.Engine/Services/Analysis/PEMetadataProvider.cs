using System;
using System.IO;
using ChaosDbg.Cordb;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using Iced.Intel;

namespace ChaosDbg.Analysis
{
    class PEMetadataProvider
    {
        private INativeDisassemblerProvider nativeDisassemblerProvider;

        public PEMetadataProvider(INativeDisassemblerProvider nativeDisassemblerProvider)
        {
            this.nativeDisassemblerProvider = nativeDisassemblerProvider;
        }

        /// <summary>
        /// Gets an object describing the PE Metadata for a <see cref="CordbNativeModule"/>.
        /// </summary>
        /// <param name="module">The module to get the metadata of.</param>
        /// <param name="options">Specifies which techniques should be used to try and locate metadata within the PE Module.</param>
        /// <returns>A <see cref="PEMetadataVirtualModule"/> that describes the specified <see cref="CordbNativeModule"/>.</returns>
        public PEMetadataVirtualModule GetVirtualMetadata(
            CordbNativeModule module,
            PEMetadataSearchOptions options)
        {
            var modulePath = Kernel32.GetModuleFileNameExW(module.Process.Handle, module.BaseAddress);

            var virtualPEFile = module.PEFile;
            var physicalPEFile = GetPhysicalPEFile(modulePath);

            var processStream = new MemoryReaderStream((IMemoryReader) module.Process.DataTarget);

            INativeDisassembler createDisassembler(Stream stream) =>
                nativeDisassemblerProvider.CreateDisassembler(stream ?? processStream, module.Process.Is32Bit, new CordbDisasmSymbolResolver(module.Process));

            var metadataModule = new PEMetadataVirtualModule(
                modulePath,
                module.BaseAddress,
                (IUnmanagedSymbolModule) module.SymbolModule,
                physicalPEFile,
                virtualPEFile,
                createDisassembler);

            var searcher = new InstructionSearcher(options, metadataModule, virtualPEFile, createDisassembler);

            searcher.Search();

            return metadataModule;
        }

        public PEMetadataPhysicalModule GetPhysicalMetadata(
            string modulePath,
            IUnmanagedSymbolModule symbolModule,
            ISymbolResolver disasmSymbolResolver,
            PEMetadataSearchOptions options,
            Action<PEFile> peFileTestHook = null)
        {
            var peSymbolResolver = new PESymbolResolver(symbolModule);

            var physicalPEFile = GetPhysicalPEFile(modulePath, PEFileDirectoryFlags.All, peSymbolResolver, peFileTestHook);

            var fileStream = File.Open(modulePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            INativeDisassembler CreateDisassembler(Stream stream) =>
                nativeDisassemblerProvider.CreateDisassembler(stream ?? fileStream, physicalPEFile.OptionalHeader.Magic == PEMagic.PE32, disasmSymbolResolver);

            var metadataModule = new PEMetadataPhysicalModule(
                modulePath,
                symbolModule,
                physicalPEFile,
                CreateDisassembler
            );

            var searcher = new InstructionSearcher(options, metadataModule, physicalPEFile, CreateDisassembler);

            searcher.Search();

            return metadataModule;
        }

        public unsafe PEMetadataVirtualModule GetVirtualMetadata(
            IntPtr hProcess,
            IntPtr hModule,
            IUnmanagedSymbolModule symbolModule,
            RemoteMemoryStream processStream,
            ISymbolResolver disasmSymbolResolver,
            PEMetadataSearchOptions options,
            Action<PEFile> peFileTestHook = null)
        {
            var modulePath = Kernel32.GetModuleFileNameExW(hProcess, hModule);

            var peSymbolResolver = new PESymbolResolver(symbolModule);

            GetPEFiles(processStream, (long) (void*) hModule, modulePath, peSymbolResolver, peFileTestHook, out var physicalPEFile, out var virtualPEFile);

            INativeDisassembler CreateDisassembler(Stream stream) =>
                nativeDisassemblerProvider.CreateDisassembler(stream ?? processStream, virtualPEFile.OptionalHeader.Magic == PEMagic.PE32, disasmSymbolResolver);

            var metadataModule = new PEMetadataVirtualModule(
                modulePath,
                hModule,
                symbolModule,
                physicalPEFile,
                virtualPEFile,
                CreateDisassembler
            );

            var searcher = new InstructionSearcher(options, metadataModule, virtualPEFile, CreateDisassembler);

            searcher.Search();

            return metadataModule;
        }

        private void GetPEFiles(
            Stream stream,
            long moduleAddress,
            string modulePath,
            IPESymbolResolver peSymbolResolver,
            Action<PEFile> peFileTestHook,
            out PEFile physicalPEFile,
            out PEFile virtualPEFile)
        {
            physicalPEFile = GetPhysicalPEFile(modulePath, peFileTestHook: peFileTestHook);

            //We don't know how big the in memory module is yet (we get that from the VirtualPEFile) and I'm not sure whether
            //we want to trust the size listed in the PhysicalPEFile. Hence, we don't copy the whole module to an in-process
            //MemoryStream to be used by both our PE File and our disassembler
            stream.Position = moduleAddress;
            virtualPEFile = PEFile.FromStream(stream, true, PEFileDirectoryFlags.All, peSymbolResolver);

            peFileTestHook?.Invoke(virtualPEFile);
        }

        private PEFile GetPhysicalPEFile(string modulePath, PEFileDirectoryFlags flags = PEFileDirectoryFlags.None, IPESymbolResolver peSymbolResolver = null, Action<PEFile> peFileTestHook = null)
        {
            //Don't need any flags here. All we're really interested in is the base address.
            var physicalPEFile = PEFile.FromPath(modulePath, flags, peSymbolResolver);

            if (physicalPEFile.OptionalHeader.Magic == PEMagic.PE32)
            {
                if (IntPtr.Size != 4)
                    throw new InvalidOperationException("Cannot process a 32-bit module inside a 64-bit process: our module path resolution logic doesn't handle SysWOW64.");
            }
            else
            {
                if (IntPtr.Size != 8)
                    throw new InvalidOperationException("Cannot process a 64-bit module inside a 32-bit process: our module path resolution logic doesn't handle sysnative.");
            }

            peFileTestHook?.Invoke(physicalPEFile);

            return physicalPEFile;
        }
    }
}
