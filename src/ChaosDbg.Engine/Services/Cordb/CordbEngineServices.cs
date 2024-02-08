using ChaosDbg.Disasm;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosLib.PortableExecutable;

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

        public ILDisassemblerProvider ILDisasmProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        public CordbEngineServices(
            IExeTypeDetector exeTypeDetector,
            NativeLibraryProvider nativeLibraryProvider,
            INativeDisassemblerProvider nativeDisasmProvider,
            ILDisassemblerProvider ilDisasmProvider,
            IPEFileProvider peFileProvider)
        {
            ExeTypeDetector = exeTypeDetector;
            NativeLibraryProvider = nativeLibraryProvider;
            NativeDisasmProvider = nativeDisasmProvider;
            ILDisasmProvider = ilDisasmProvider;
            PEFileProvider = peFileProvider;
        }
    }
}
