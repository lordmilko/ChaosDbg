using ChaosDbg.Disasm;
using ChaosLib.Metadata;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Encapsulates critical services required by a <see cref="CordbEngine"/> and its related entities.
    /// </summary>
    public class CordbEngineServices
    {
        public INativeDisassemblerProvider NativeDisasmProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        public CordbEngineServices(
            INativeDisassemblerProvider nativeDisasmProvider,
            IPEFileProvider peFileProvider)
        {
            NativeDisasmProvider = nativeDisasmProvider;
            PEFileProvider = peFileProvider;
        }
    }
}
