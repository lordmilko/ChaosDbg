using ChaosDbg.Metadata;
using ChaosLib;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Encapsulates critical services required by a <see cref="CordbEngine"/> and its related entities.
    /// </summary>
    public class CordbEngineServices
    {
        public IFrameworkTypeDetector FrameworkTypeDetector { get; }

        public INativeLibraryProvider NativeLibraryProvider { get; }

        public ISymSrv SymSrv { get; }

        public IUserInterface UserInterface { get; }

        public CordbEngineServices(
            IFrameworkTypeDetector frameworkTypeDetector,
            INativeLibraryProvider nativeLibraryProvider,
            ISymSrv symSrv,
            IUserInterface userInterface)
        {
            FrameworkTypeDetector = frameworkTypeDetector;
            NativeLibraryProvider = nativeLibraryProvider;
            SymSrv = symSrv;
            UserInterface = userInterface;
        }
    }
}
