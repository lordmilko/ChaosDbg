using ChaosDbg.Disasm;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols.MicrosoftPdb;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Encapsulates critical services required by a <see cref="CordbEngine"/> and its related entities.
    /// </summary>
    public class CordbEngineServices
    {
        public IFrameworkTypeDetector FrameworkTypeDetector { get; }

        public NativeLibraryProvider NativeLibraryProvider { get; }

        public INativeDisassemblerProvider NativeDisasmProvider { get; }

        public ILDisassemblerProvider ILDisasmProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        public MicrosoftPdbSourceFileProvider MicrosoftPdbSourceFileProvider { get; }

        public CordbEngineServices(
            IFrameworkTypeDetector frameworkTypeDetector,
            NativeLibraryProvider nativeLibraryProvider,
            INativeDisassemblerProvider nativeDisasmProvider,
            ILDisassemblerProvider ilDisasmProvider,
            IPEFileProvider peFileProvider,
            MicrosoftPdbSourceFileProvider microsoftPdbSourceFileProvider)
        {
            FrameworkTypeDetector = frameworkTypeDetector;
            NativeLibraryProvider = nativeLibraryProvider;
            NativeDisasmProvider = nativeDisasmProvider;
            ILDisasmProvider = ilDisasmProvider;
            PEFileProvider = peFileProvider;
            MicrosoftPdbSourceFileProvider = microsoftPdbSourceFileProvider;
        }
    }
}
