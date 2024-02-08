using ChaosLib.PortableExecutable;

namespace ChaosDbg.DbgEng
{
    public class DbgEngEngineServices
    {
        public NativeLibraryProvider NativeLibraryProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        public DbgEngEngineServices(
            NativeLibraryProvider nativeLibraryProvider,
            IPEFileProvider peFileProvider)
        {
            NativeLibraryProvider = nativeLibraryProvider;
            PEFileProvider = peFileProvider;
        }
    }
}
