using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ChaosLib;
using ChaosLib.Symbols;

#nullable enable

namespace ChaosDbg.SymStore
{
    /// <summary>
    /// Represents a fake SYMSRV.DLL for the purposes of performing thread safe concurrent symbol loading.
    /// </summary>
    public class FakeSymSrv : ISymSrv
    {
        #region Delegates

        delegate bool SymbolServerCloseDelegate();

        delegate bool SymbolServerDeltaNameWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string a,
            [In] IntPtr b,
            [In] int c,
            [In] int d,
            [In] IntPtr e,
            [In] int f,
            [In] int g,
            [In, MarshalAs(UnmanagedType.LPWStr)] string h,
            [In] IntPtr i);

        delegate bool SymbolServerGetIndexStringWDelegate(
            [In] IntPtr a,
            [In] int b,
            [In] int c,
            [Out] IntPtr d,
            [In] IntPtr e);

        delegate IntPtr SymbolServerGetOptionsDelegate();

        delegate bool SymbolServerGetOptionDataDelegate(
            [In] IntPtr option,
            [Out] out long pData);

        delegate bool SymbolServerGetSupplementWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string a,
            [In, MarshalAs(UnmanagedType.LPWStr)] string b,
            [In, MarshalAs(UnmanagedType.LPWStr)] string c,
            [Out] IntPtr d,
            [In] IntPtr e);

        delegate bool SymbolServerIsStoreWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string path);

        delegate bool SymbolServerPingWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string @params);

        delegate bool SymbolServerPingWExDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pParameters);

        delegate bool SymbolServerSetOptionsDelegate(
            [In] IntPtr options, //SSRVOPT
            [In] long data);

        delegate bool SymbolServerSetOptionsWDelegate(
            [In] IntPtr options,
            [In] long data);

        delegate bool SymbolServerStoreFileWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string SrvPath,
            [In, MarshalAs(UnmanagedType.LPWStr)] string FileName,
            [In] IntPtr id,
            [In] int val2,
            [In] int val3,
            [Out] IntPtr StoredPath,
            [In] IntPtr cStoredPath,
            [In] SYMSTOREOPT Flags);

        delegate bool MissingSymbolServerStoreSupplementWDelegate();

        delegate bool SymbolServerWDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string @params,
            [In, MarshalAs(UnmanagedType.LPWStr)] string filename,
            [In] IntPtr id,
            [In] int two,
            [In] int three,
            [Out] IntPtr path);

        delegate bool SymbolServerWExDelegate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string @params,
            [In, MarshalAs(UnmanagedType.LPWStr)] string filename,
            [In] IntPtr id,
            [In] int two,
            [In] int three,
            [Out] IntPtr path,
            [Out] out SYMSRV_EXTENDED_OUTPUT_DATA _g);

        #endregion
        #region Fields

        private SymbolServerCloseDelegate symbolServerClose;
        private SymbolServerDeltaNameWDelegate symbolServerDeltaNameW;
        private SymbolServerGetIndexStringWDelegate symbolServerGetIndexStringW;
        private SymbolServerGetOptionsDelegate symbolServerGetOptions;
        private SymbolServerGetOptionDataDelegate symbolServerGetOptionData;
        private SymbolServerGetSupplementWDelegate symbolServerGetSupplementW;
        private SymbolServerIsStoreWDelegate symbolServerIsStoreW;
        private SymbolServerPingWDelegate symbolServerPingW;
        private SymbolServerPingWExDelegate symbolServerPingWEx;
        private SymbolServerSetOptionsDelegate symbolServerSetOptions;
        private SymbolServerSetOptionsWDelegate symbolServerSetOptionsW;
        private SymbolServerStoreFileWDelegate symbolServerStoreFileW;
        private MissingSymbolServerStoreSupplementWDelegate symbolServerStoreSupplementW;
        private SymbolServerWDelegate symbolServerW;
        private SymbolServerWExDelegate symbolServerWEx;

        private IntPtr fnSymbolServerClose;
        private IntPtr fnSymbolServerDeltaNameW;
        private IntPtr fnSymbolServerGetIndexStringW;
        private IntPtr fnSymbolServerGetOptions;
        private IntPtr fnSymbolServerGetOptionData;
        private IntPtr fnSymbolServerGetSupplementW;
        private IntPtr fnSymbolServerIsStoreW;
        private IntPtr fnSymbolServerPingW;
        private IntPtr fnSymbolServerPingWEx;
        private IntPtr fnSymbolServerSetOptions;
        private IntPtr fnSymbolServerSetOptionsW;
        private IntPtr fnSymbolServerStoreFileW;
        private IntPtr fnSymbolServerStoreSupplementW;
        private IntPtr fnSymbolServerW;
        private IntPtr fnSymbolServerWEx;

        #endregion

        [ThreadStatic]
        private static SymSrvScope? scope;

        //todo: not sure whether this should be thread static? will dia set the options each time it does a request?
        //the same question also applies to dbghelp
        [Obsolete]
        private SSRVOPT globalOptions = SSRVOPT.SSRVOPT_DWORD;
        [Obsolete]
        private SSRVOPT globalType = SSRVOPT.SSRVOPT_DWORD;
        private string globalDownstreamStore;

        public FakeSymSrv()
        {
            symbolServerClose            = SymbolServerClose;
            symbolServerDeltaNameW       = SymbolServerDeltaNameW;
            symbolServerGetIndexStringW  = SymbolServerGetIndexStringW;
            symbolServerGetOptions       = SymbolServerGetOptions;
            symbolServerGetOptionData    = SymbolServerGetOptionData;
            symbolServerGetSupplementW   = SymbolServerGetSupplementW;
            symbolServerIsStoreW         = SymbolServerIsStoreW;
            symbolServerPingW            = SymbolServerPingW;
            symbolServerPingWEx          = SymbolServerPingWEx;
            symbolServerSetOptions       = SymbolServerSetOptions;
            symbolServerSetOptionsW      = SymbolServerSetOptionsW;
            symbolServerStoreFileW       = SymbolServerStoreFileW;
            symbolServerStoreSupplementW = MissingSymbolServerStoreSupplementW;
            symbolServerW                = SymbolServerW;
            symbolServerWEx              = SymbolServerWEx;

            fnSymbolServerClose            = Marshal.GetFunctionPointerForDelegate(symbolServerClose);
            fnSymbolServerDeltaNameW       = Marshal.GetFunctionPointerForDelegate(symbolServerDeltaNameW);
            fnSymbolServerGetIndexStringW  = Marshal.GetFunctionPointerForDelegate(symbolServerGetIndexStringW);
            fnSymbolServerGetOptions       = Marshal.GetFunctionPointerForDelegate(symbolServerGetOptions);
            fnSymbolServerGetOptionData    = Marshal.GetFunctionPointerForDelegate(symbolServerGetOptionData);
            fnSymbolServerGetSupplementW   = Marshal.GetFunctionPointerForDelegate(symbolServerGetSupplementW);
            fnSymbolServerIsStoreW         = Marshal.GetFunctionPointerForDelegate(symbolServerIsStoreW);
            fnSymbolServerPingW            = Marshal.GetFunctionPointerForDelegate(symbolServerPingW);
            fnSymbolServerPingWEx          = Marshal.GetFunctionPointerForDelegate(symbolServerPingWEx);
            fnSymbolServerSetOptions       = Marshal.GetFunctionPointerForDelegate(symbolServerSetOptions);
            fnSymbolServerSetOptionsW      = Marshal.GetFunctionPointerForDelegate(symbolServerSetOptionsW);
            fnSymbolServerStoreFileW       = Marshal.GetFunctionPointerForDelegate(symbolServerStoreFileW);
            fnSymbolServerStoreSupplementW = Marshal.GetFunctionPointerForDelegate(symbolServerStoreSupplementW);
            fnSymbolServerW                = Marshal.GetFunctionPointerForDelegate(symbolServerW);
            fnSymbolServerWEx              = Marshal.GetFunctionPointerForDelegate(symbolServerWEx);
        }

        /// <summary>
        /// Retrieves a file from the symbol server.
        /// </summary>
        /// <param name="params">The symbol path to use, with any prefix (such as SRV*) removed. e.g. 'c:\symbols*http://msdl.microsoft.com/download/symbols'</param>
        /// <param name="filename">The name of the PDB to locate</param>
        /// <param name="id">The first parameter to use when constructing the path to the target file. The meaning of this parameter depends on the type stored in <see cref="globalType"/>.
        /// Usually, <see cref="globalType"/> will be <see cref="SSRVOPT.SSRVOPT_GUIDPTR"/>, indicating that this is a pointer to a GUID.</param>
        /// <param name="two">The second value to use when constructing the directory name. Usually this is the PDB Age</param>
        /// <param name="three">The third value to use when constructing the directory name. This tends to either be the PDB age or the size of the image.</param>
        /// <param name="path">A buffer containing at least MAX_PATH characters that the found file should be written to on success.</param>
        /// <returns>True if a file was successfully found, otherwise false.</returns>
        private bool SymbolServerW(string @params, string filename, IntPtr id, int two, int three, IntPtr path)
        {
            /* DbgHelp uses a completely non-sensical algorithm for searching for symbols, wherein it will ask symsrv.dll *first* prior to checking basic places
             * on the local filesystem */

            //When attempting to resolve certain PDBs (in particular, those that are part of .NET Core), after attempting to load the symbol using the age specified in the debug directory,
            //LOCATOR tries again, specifying the age as -1. -1 does _not_ come from the PE file. It decides to try again using -1 when a certain flag is set. My psychic powers predict
            //that maybe it may be because the Reproducible directory is present

            string result;

            //If we executed FakeSymSrv previously, or we otherwise located a PDB without calling into FakeSymSrv, save a bunch of effort
            //and just return the already known file

            if (scope != null)
            {
                if (scope.FoundPdb != null)
                {
                    result = scope.FoundPdb;

                    goto success;
                }
            }

            //We're either DIA executing the initial request for symbols, or DbgHelp, and DIA wasn't able to find a PDB

            var key = GetKey(filename, id, two, three);

            Dictionary<string, string>? locationCache = null;

            if (scope != null)
            {
                if (scope.TryGetCachedValue(@params, key.Index, out locationCache, out result))
                {
                    //No symbol was found last time we checked, no point checking again
                    if (result == null)
                        return false;

                    goto success;
                }
            }

            //It's a brand new request

            var client = new SymbolClient(new NullSymStoreLogger(), @params);

            if (!client.TryGetStoreFile(key, scope?.CancellationToken ?? default, out result))
            {
                if (locationCache != null)
                {
                    //If anyone else tries to locate the symbol under this location, tell them there's no need, the file doesn't exist
                    locationCache[key.Index] = result;
                }

                return false;
            }

            if (result.Length > Kernel32.MAX_PATH)
        private bool SymbolServerPingWEx(string pParameters)
        {
            //Whatever path they're asking for, assume it's valid

            //Any path that we agree to, DbgHelp will pass to SymbolServerW in an attempt to locate symbols. DbgHelp prepends "." so its internal symbol path,
            //however symsrv!SymbolServerPingWEx returns false for this path, and SymbolServerW is never called with the params set to "."
            //We generalize this to say that if a segment of the search path doesn't have a * in it, symsrv doesn't need to know about it

            if (!pParameters.Contains("*"))
                return false;

            return true;
        }
    }
}
