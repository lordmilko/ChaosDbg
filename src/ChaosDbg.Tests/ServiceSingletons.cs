using ChaosDbg.DbgEng;
using ChaosDbg.Symbols;
using ChaosDbg.SymStore;
using ChaosLib;
using SymHelp.Symbols;

namespace ChaosDbg.Tests
{
    static class ServiceSingletons
    {
        public static readonly ISymSrv SymSrv = new FakeSymSrv();
        public static readonly DbgEngNativeLibraryLoadCallback DbgEngNativeLibraryLoadCallback = new();
        public static readonly DbgHelpNativeLibraryLoadCallback DbgHelpNativeLibraryLoadCallback = new(SymSrv);
        public static readonly MSDiaNativeLibraryLoadCallback MSDiaNativeLibraryLoadCallback = new(SymSrv);
        public static readonly NativeLibraryProvider NativeLibraryProvider = new(callbacks: new INativeLibraryLoadCallback[] {DbgEngNativeLibraryLoadCallback, DbgHelpNativeLibraryLoadCallback, MSDiaNativeLibraryLoadCallback});
        public static readonly NativeReflector NativeReflector = new NativeReflector(NativeLibraryProvider, SymSrv);

        public static void Dispose()
        {
            //Note that if you run ChaosDbg under Application Verifier, when msdia140 is unloaded
            //and you're tracking leaks, you'll get an error, because DIA is leaking memory. But
            //that's not our problem, and so Application Verifier should be run with leak detection disabled

            NativeReflector.Dispose();
            NativeLibraryProvider.Dispose();
        }
    }
}
