using System;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    public class DbgEngEngineServices
    {
        public NativeLibraryProvider NativeLibraryProvider { get; }

        public IPEFileProvider PEFileProvider { get; }

        internal DbgEngNativeLibraryLoadCallback DbgEngNativeLibraryLoadCallback { get; }

        internal DbgEngEngineServices(
            NativeLibraryProvider nativeLibraryProvider,
            IPEFileProvider peFileProvider,
            DbgEngNativeLibraryLoadCallback dbgEngNativeLibraryLoadCallback)
        {
            NativeLibraryProvider = nativeLibraryProvider;
            PEFileProvider = peFileProvider;
            DbgEngNativeLibraryLoadCallback = dbgEngNativeLibraryLoadCallback;
        }

        [Obsolete("DbgEng is not thread safe. Attempting to use multiple debuggers concurrently will cause g_Machine to be overwritten. Ensure only a single thread utilizes a DebugClient at a time")]
        public DebugClient SafeDebugCreate(bool allowOverwriteSymbolOpts)
        {
            return DebugCreateOrConnect<DebugCreateDelegate>(
                d =>
                {
                    d(DebugClient.IID_IDebugClient, out var pDebugClient).ThrowDbgEngNotOK();

                    var client = new DebugClient(pDebugClient);

                    Log.Debug<DebugClient>("Created DebugCreate DebugClient {hashCode}", client.GetHashCode());

                    return client;
                },
                "DebugCreate",
                allowOverwriteSymbolOpts
            );
        }

        public DebugClient SafeDebugConnect(string remoteOptions, bool allowOverwriteSymbolOpts)
        {
            return DebugCreateOrConnect<DebugConnectDelegate>(
                d =>
                {
                    d(remoteOptions, DebugClient.IID_IDebugClient, out var pDebugClient).ThrowDbgEngNotOK();

                    var client = new DebugClient(pDebugClient);

                    Log.Debug<DebugClient>("Created DebugConnect DebugClient {hashCode}", client.GetHashCode());

                    return client;
                },
                "DebugConnect",
                allowOverwriteSymbolOpts
            );
        }

        public void EnableTestHook()
        {
            DebugCreateOrConnect<DebugCreateDelegate>(d =>
            {
                d(typeof(IDebugTestHook).GUID, out var pTestHook).ThrowDbgEngNotOK();

                var testHook = new DebugTestHook((IDebugTestHook) Marshal.GetObjectForIUnknown(pTestHook));

                testHook.SetValue(DEBUG_HOOK_INDEX.ALLOW_QI_IUNKNOWN, 1);

                return null;
            }, "DebugCreate", false);
        }

        private DebugClient DebugCreateOrConnect<TDelegate>(Func<TDelegate, DebugClient> createClient, string procName, bool allowOverwriteSymbolOpts)
        {
            /* We don't call SetDllDirectory for DbgEng, as
             *     a. we reserve that for when ChaosDbg self extracts itself, and
             *     b. we don't actally need to
             *
             * You can't use AddDllDirectory because it doesn't actually do anything when the user doesn't opt into a very specific
             * way of calling LoadLibraryEx(). When dbghelp.dll looks for symsrv.dll, it specifically constraints its search to the same
             * directory that dbghelp.dll is in. Thus, simply loading dbghelp.dll from the desired directory is enough to load symsrv.dll
             * from the same place as well. But when dbgeng!DebugCreate calls into SymInitialize from dbgeng!ProcessInfo::ProcessInfo,
             * the operating system will just look for dbghelp.dll wherever it likes, which may result in the copy from our bundled
             * native dependencies being loaded rather than the one from the system installed version (when we prefer the system
             * installed version over our own one)
             *
             * Thus, to mitigate this we force load DbgHelp prior to DbgEng to ensure its loaded */

            //Force load DbgHelp from the same directory as DbgEng, so that DbgEng doesn't try and load it from some other directory (such as our output folder if we're
            //trying to use the system DbgEng installation)
            NativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            var debugCreateOrConnect = NativeLibraryProvider.GetExport<TDelegate>(WellKnownNativeLibrary.DbgEng, procName);

            /* If we're trying to use DbgHelp ourselves, we may cause crashes if DbgEng tries to do its own things with DbgHelp at the same
             * time that we do. As such, hook all DbgHelp functions imported by DbgEng that operate on a hProcess.
             *
             * DebugCreate/DebugConnect will both call dbgeng!OneTimeInitialization which will overwrite our symbol options.
             * This is particularly troublesome since DbgEng prefers to use deferred symbol loads, while we may prefer eager. We can't
             * simply store and revert the old options after DebugCreate/DebugConnect returns, because in the meantime one of our other
             * threads may be trying to do stuff with DbgHelp, and this will mess up their options. Thus, we solve this by intercepting
             * DbgEng's call to SymSetOptions and denying the request. */
            DbgEngNativeLibraryLoadCallback.AllowOverwriteSymbolOpts = allowOverwriteSymbolOpts;

            var debugClient = createClient(debugCreateOrConnect);

            return debugClient;
        }
    }
}
