using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg
{
#if DEBUG
    public static class WellKnownSymbol
    {
        public static class DbgHelp
        {
            //Contains information about the GLOBALS structure in DbgHelp, as deduced from symbols contained in IDA Pro,
            //functions that reference these fields and the names of the parameters that are used to assign values to them
            public struct GLOBALS
            {
                public IntPtr _unknown1;

                public IntPtr _unknown2;

                public int TlsIndex;

                //dbghelp!DllMain calls GetVersionExA and sets this
                public OSVERSIONINFOA OSVersionInfo;

                //ImagehlpApiVersionEx does something with this
                public API_VERSION ApiVersionEx;

                //ImagehlpApiVersion returns this
                public API_VERSION ApiVersion;

                //StackWalk64 assigns to this
                public IMAGE_FILE_MACHINE MachineType;

                //References by SymCleanup
                public CRITICAL_SECTION CriticalSection;

                //from dbghelp!symsrvLoadLib
                public IntPtr hSymSrv;
                public IntPtr fnSymbolServerW;
                public IntPtr fnSymbolServerClose;
                public IntPtr fnSymbolServerSetOptionsW;
                public IntPtr fnSymbolServerPingW;
                public IntPtr fnSymbolServerDeltaNameW;
                public IntPtr fnSymbolServerGetSupplementW;
                public IntPtr fnSymbolServerStoreSupplementW;
                public IntPtr fnSymbolServerGetIndexStringW;
                public IntPtr fnSymbolServerStoreFileW;
                public IntPtr fnSymbolServerIsStoreW;
                public IntPtr fnSymbolServerGetOptions;
                public IntPtr fnSymbolServerGetOptionData;

                //from dbghelp!srcsrvInit
                public IntPtr hSrcSrv;
                public IntPtr fnSrcSrvInitW;
                public IntPtr fnSrcSrvCleanup;
                public IntPtr fnSrcSrvSetTargetPathW;
                public IntPtr fnSrcSrvSetOptions;
                public IntPtr fnSrcSrvGetOptions;
                public IntPtr fnSrcSrvLoadModuleW;
                public IntPtr fnSrcSrvLoadModuleWithSourceCollectionW;
                public IntPtr fnSrcSrvBuildTokenFromWebUrlList;
                public IntPtr fnSrcSrvUnloadModule;
                public IntPtr fnSrcSrvRegisterCallback;
                public IntPtr fnSrcSrvGetFileW;
                public IntPtr fnSrcSrvGetTokenW;
                public IntPtr fnSrcSrvExecTokenByTokenNameW;
                public IntPtr fnSrcSrvResolveTokenVarW;
                public IntPtr fnSrcSrvSetParentWindow;
                public IntPtr fnSrcSrvEnumTokens;

                public IntPtr NumProcesses; //int + padding
                public LIST_ENTRY Processes;
                public int _unknown3;
                public SYMOPT SymOptions;

                public PROCESS_ENTRY[] ProcessEntries
                {
                    get
                    {
                        var results = new PROCESS_ENTRY[(int) NumProcesses];

                        LIST_ENTRY current = Processes;

                        for (var i = 0; i < (int) NumProcesses; i++)
                        {
                            var value = Marshal.PtrToStructure<PROCESS_ENTRY>(current.Flink);

                            if (i == (int) NumProcesses - 1)
                                results[0] = value;
                            else
                                results[i + 1] = value;

                            current = value.ListEntry;
                        }

                        return results;
                    }
                }
            }

            [DebuggerDisplay("hProcess = {hProcess.ToString(\"X\")}")]
            public struct PROCESS_ENTRY
            {
                public LIST_ENTRY ListEntry;
                public LIST_ENTRY Modules;
                public IntPtr _unknown1;
                public int RefCount;
                public IntPtr hProcess;
                public int ProcessId;
            }
        }
    }
#endif
}
