using ChaosDbg.Cordb;
using ChaosLib.Metadata;

namespace ChaosDbg
{
    /// <summary>
    /// Encapsulates critical services required by a <see cref="CordbEngine"/> and its related entities.
    /// </summary>
    public class CordbEngineServices
    {
        public IPEFileProvider PEFileProvider { get; }

        public CordbEngineServices(IPEFileProvider peFileProvider)
        {
            PEFileProvider = peFileProvider;
        }
    }
}
