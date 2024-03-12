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

        public DbgEngEngineServices(
            NativeLibraryProvider nativeLibraryProvider,
            IPEFileProvider peFileProvider)
        {
            NativeLibraryProvider = nativeLibraryProvider;
            PEFileProvider = peFileProvider;
        }

        public DebugClient SafeDebugCreate(bool allowOverwriteSymbolOpts)
        {
            return DebugCreateOrConnect<DebugCreateDelegate>(
                d =>
                {
                    d(DebugClient.IID_IDebugClient, out var pDebugClient).ThrowDbgEngNotOK();

                    return new DebugClient(pDebugClient);
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

                    return new DebugClient(pDebugClient);
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

            NativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            /* DebugCreate/DebugConnect will both call dbgeng!OneTimeInitialization which will overwrite our symbol options.
             * This is particularly troublesome since DbgEng prefers to use deferred symbol loads, while we may prefer eager. Thus,
             * if we don't want to allow DbgEng to control our symbol options, we'll revert them back after we create our client */
            var originalOpts = DbgHelp.SymGetOptions();

            var debugCreateOrConnect = NativeLibraryProvider.GetExport<TDelegate>(WellKnownNativeLibrary.DbgEng, procName);

            var debugClient = createClient(debugCreateOrConnect);

            if (!allowOverwriteSymbolOpts)
                DbgHelp.SymSetOptions(originalOpts);

            return debugClient;
        }
    }
}
