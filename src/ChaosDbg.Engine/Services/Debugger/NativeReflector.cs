using ChaosDbg.TypedData;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.TypedData;
using ClrDebug;

namespace ChaosDbg
{
    class NativeReflector
    {
        private static NativeReflector instance = new NativeReflector();
        private NativeLibraryProvider nativeLibraryProvider;

        public static IDbgRemoteObject GetTypedData<T>(ComObject<T> comObject)
        {
            return instance.dbgHelp.TypedDataProvider.CreateObjectForRCW(comObject.Raw);
        }

        private NativeReflector()
        {
            nativeLibraryProvider = new NativeLibraryProvider(
                callbacks: new[] {new DbgHelpNativeLibraryLoadCallback()}
            );

            nativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            //TEMP. we need to be able to resolve the module to use for the session without having to hardcode it
            dbgHelp = DbgHelpProvider.Acquire(Kernel32.Native.GetCurrentProcess(), typedDataModelProvider: new DiaTypedDataModelProvider(() => dbgHelp));
        }

        private IDbgHelp dbgHelp;

        ~NativeReflector()
        {
            Log.Disable();
            dbgHelp.Dispose();
            nativeLibraryProvider.Dispose();
        }
    }
}
