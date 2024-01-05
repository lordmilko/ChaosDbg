using ChaosLib.Metadata;

namespace ChaosDbg.DbgEng
{
    public class DbgEngEngineServices
    {
        public IPEFileProvider PEFileProvider { get; }

        public DbgEngEngineServices(IPEFileProvider peFileProvider)
        {
            PEFileProvider = peFileProvider;
        }
    }
}
