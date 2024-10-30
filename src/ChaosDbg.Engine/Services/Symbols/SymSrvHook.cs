using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChaosDbg.Hook;
using ChaosLib;
using ChaosLib.Detour;
using SymHelp.Symbols;

namespace ChaosDbg.Symbols
{
    /// <summary>
    /// Encapsulates a LoadLibraryExW and GetProcAddress hook against symsrv.dll for a given module,
    /// such that calls made into exports provided by symsrv.dll can be intercepted by ChaosDbg.
    /// </summary>
    class SymSrvHook
    {
        private ImportHook loadLibraryExWImportHook;
        private LoadLibraryExWConditionalHook loadLibraryExWConditionalHook;

        private ImportHook getProcAddressImportHook;
        private GetProcAddressConditionalHook getProcAddressConditionalHook;

        private ISymSrv symSrv;
        private string symSrvPath;

        private int refCount = 1;
            loadLibraryExWImportHook = new ImportHook(
                hModule,
                null,
                "LoadLibraryExW",
                address =>
                {
                    Debug.Assert(loadLibraryExWConditionalHook == null);
                    loadLibraryExWConditionalHook = new LoadLibraryExWConditionalHook(symSrvPath, address, LoadLibraryExWHook);
                    return loadLibraryExWConditionalHook.pTrampoline;
                }
            );

            getProcAddressImportHook = new ImportHook(
                hModule,
                null,
                "GetProcAddress",
                address =>
                {
                    Debug.Assert(getProcAddressConditionalHook == null);
                    getProcAddressConditionalHook = new GetProcAddressConditionalHook(Marshal.GetHINSTANCE(GetType().Module), address, GetProcAddressHook);
                    return getProcAddressConditionalHook.pTrampoline;
                }
            );
        }
        private IntPtr LoadLibraryExWHook(string lpLibFileName, IntPtr hFile, LoadLibraryFlags dwFlags)
        {
            Debug.Assert(lpLibFileName == symSrvPath);

            //We assume the identity of SYMSRV.DLL. Any and all requests for procedures from SYMSRV.DLL should get routed to us
            return Marshal.GetHINSTANCE(GetType().Module);
        }

        private IntPtr GetProcAddressHook(IntPtr hModule, string name)
        {
            Debug.Assert(hModule == Marshal.GetHINSTANCE(GetType().Module));

            return symSrv.GetProcAddress(name);
        }

        //These occur under a lock
        public void AddRef() => refCount++;

        public bool Release()
        {
            refCount--;

            if (refCount == 0)
            {
                loadLibraryExWConditionalHook.Hooked = false;
                getProcAddressConditionalHook.Hooked = false;

                return true;
            }

            return false;
        }
    }
}
