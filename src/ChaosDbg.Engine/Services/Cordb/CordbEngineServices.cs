using ChaosDbg.Disasm;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Encapsulates critical services required by a <see cref="CordbEngine"/> and its related entities.
    /// </summary>
    public class CordbEngineServices
    {
        public IExeTypeDetector ExeTypeDetector { get; }

        public NativeLibraryProvider NativeLibraryProvider { get; }

        public INativeDisassemblerProvider NativeDisasmProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        public CordbEngineServices(
            IExeTypeDetector exeTypeDetector,
            NativeLibraryProvider nativeLibraryProvider,
            INativeDisassemblerProvider nativeDisasmProvider,
            IPEFileProvider peFileProvider)
        {
            ExeTypeDetector = exeTypeDetector;
            NativeLibraryProvider = nativeLibraryProvider;
            NativeDisasmProvider = nativeDisasmProvider;
            PEFileProvider = peFileProvider;
        }
    }
}
