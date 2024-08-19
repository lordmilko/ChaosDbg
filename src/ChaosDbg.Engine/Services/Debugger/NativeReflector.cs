using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ChaosDbg.TypedData;
using ChaosLib;
using ChaosLib.Detour;
using ChaosLib.TypedData;
using ClrDebug;
using ClrDebug.DIA;

namespace ChaosDbg
{
#if DEBUG
    /// <summary>
    /// Provides facilities for interacting with native symbols that have been loaded into ChaosDbg itself.
    /// </summary>
    class NativeReflector
    {
        private static NativeReflector instanceRaw;

        private static NativeReflector instance
        {
            get
            {
                Debug.Assert(instanceRaw != null, $"{nameof(NativeReflector)} has not been initialized");
                return instanceRaw;
            }
        }

        public static void Initialize(
            NativeLibraryProvider nativeLibraryProvider,
            IDbgHelpProvider dbgHelpProvider)
        {
            instanceRaw = new NativeReflector();

            nativeLibraryProvider.GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            instanceRaw.dbgHelp = dbgHelpProvider.Acquire(Kernel32.Native.GetCurrentProcess(), typedDataModelProvider: new DiaTypedDataModelProvider(() => instanceRaw.dbgHelp), description: "NativeReflector");
        }

        public static IDbgRemoteObject GetTypedData<T>(ComObject<T> comObject)
        {
            //This will load the module if it isn't loaded
            return instance.dbgHelp.TypedDataProvider.CreateObjectForRCW(comObject.Raw);
        }

        public static void InstallThunkAwareHook<TDelegate, THook>(string pattern, THook hook)
            where TDelegate : Delegate
            where THook : Delegate
        {
            //Many symbols may have multiple results, all with the same address. Furthermore, calling SymSearch against our OutOfProcDbgHelp is slow,
            //so we use a separate method for this

            EnsureDbgHelp(pattern, out var moduleBase, out _, out var symbolName);

            long GetBestSymbolToHook()
            {
                //Certain symbols are quite troublesome. Every DebugClient::AddRef is reported by DIA in the exact same way for both the actual method
                //and every underlying adjustor thunk. As such, we need to enumerate all candidate symbols first. If there's only a single match, we
                //can use that. Otherwise, we have to use DIA to try and figure out the correct method to use

                var dbgHelpCandidates = new List<SymbolInfo>();

                instance.dbgHelp.SymSearch(moduleBase, symbol =>
                {
                    dbgHelpCandidates.Add(symbol);
                    return true;
                }, SYMSEARCH.ALLITEMS, mask: symbolName);

                if (dbgHelpCandidates.Count == 0)
                    throw new InvalidOperationException($"Could not find any symbols that match the pattern '{pattern}'");

                if (dbgHelpCandidates.Count == 1)
                    return dbgHelpCandidates[0].Address;

                //Use DIA to figure out the best symbol
                var diaSession = instance.dbgHelp.SymGetDiaSession(moduleBase);

                var diaSymbols = dbgHelpCandidates.Select(v => (dbgHelp: v, dia: diaSession.SymbolById(v.Index))).ToArray();

                var diaCandidates = new List<(SymbolInfo dbgHelp, DiaSymbol dia)>();

                foreach (var symbol in diaSymbols)
                {
                    var undecoratedName = symbol.dia.UndecoratedName;

                    if (undecoratedName.Contains("thunk"))
                        continue;

                    diaCandidates.Add(symbol);
                }

                if (diaCandidates.Count == 0)
                    throw new InvalidOperationException($"Could not find any non-thunk symbols that match the pattern '{pattern}'");

                if (diaCandidates.Count == 1)
                    return diaCandidates[0].dbgHelp.Address;

                throw new InvalidOperationException($"Found multiple non-thunk symbols that match the pattern '{pattern}'");
            }

            var address = GetBestSymbolToHook();

            var original = Marshal.GetDelegateForFunctionPointer<TDelegate>((IntPtr) address);

            DetourBuilder.AddSymbolHook(original, hook);
        }

        public static void InstallHook<TDelegate, THook>(string pattern, THook hook)
            where TDelegate : Delegate
            where THook : Delegate
        {
            EnsureDbgHelp(pattern, out _, out _, out _);

            if (instance.dbgHelp.TrySymFromName(pattern, out var symbol) != HRESULT.S_OK)
                throw new InvalidOperationException($"Could not find any symbols that match the pattern '{pattern}'");

            var original = Marshal.GetDelegateForFunctionPointer<TDelegate>((IntPtr) symbol.Address);

            DetourBuilder.AddSymbolHook(original, hook);
        }

        public static unsafe IDbgRemoteField[] GetGlobals(string pattern)
        {
            EnsureDbgHelp(pattern, out var moduleBase, out _, out var symbolName);

            var results = new List<IDbgRemoteField>();

            instance.dbgHelp.SymSearch(moduleBase, info =>
            {
                IDbgRemoteField field;

                if (info.TypeIndex == 0)
                {
                    //I think this means that symbols have been stripped. Make it a void*

                    field = DbgHelpRemoteField.New(
                        info.Address,
                        new DbgHelpRemoteFieldInfo(
                            info.Name,
                            new DbgRemoteType(
                                tag: SymTagEnum.PointerType,
                                baseType: new DbgRemoteType(
                                    basicType: BasicType.btVoid
                                )
                            )
                        ),
                        null,
                        instance.dbgHelp.TypedDataProvider
                    );
                }
                else
                {
                    var type = DbgHelpRemoteType.New(moduleBase, info.TypeIndex, instance.dbgHelp.TypedDataProvider);

                    var value = instance.dbgHelp.TypedDataProvider.CreateValue(info.Address, type, null);

                    //Not sure where the field would come from, or if this is the right thing to do?
                    //field = DbgHelpRemoteField.New(info.Address, new DbgHelpRemoteFieldInfo());
                    throw new NotImplementedException("Retrieving a global that has proper type symbols is not implemented");
                }

                results.Add(field);

                return true;
            }, SYMSEARCH.ALLITEMS, mask: symbolName);

            return results.OrderBy(v => v.Name).ToArray();
        }

        /// <summary>
        /// Reads the value of a global field and marshals it to a user defined type that approximately models the native value
        /// </summary>
        /// <typeparam name="T">A type that approximately models the native value</typeparam>
        /// <param name="pattern">A pattern in the form &lt;module&gt;!&lt;name&gt; that specifies the global symbol whose value should be retrieved.</param>
        /// <returns>The value of the specified global symbol.</returns>
        public static T GetGlobal<T>(string pattern) where T : struct
        {
            EnsureDbgHelp(pattern, out _, out _, out _);

            var symbol = instance.dbgHelp.SymFromName(pattern);

            var result = Marshal.PtrToStructure<T>((IntPtr) symbol.Address);

            return result;
        }

        public static T GetGlobal<T>(IntPtr address)
        {
            var result = Marshal.PtrToStructure<T>(address);

            return result;
        }

        private static unsafe void EnsureDbgHelp(string pattern, out long moduleBase, out string moduleName, out string symbolName)
        {
            var exclamation = pattern.IndexOf('!');

            if (exclamation <= 0)
                throw new ArgumentException("Pattern '{pattern}' is invalid: could not find a '!' delimiting the module and symbol name", nameof(pattern));

            moduleName = pattern.Substring(0, exclamation);
            symbolName = pattern.Substring(exclamation + 1);

            moduleBase = (long) (void*) Kernel32.GetModuleHandleW(moduleName);

            if (instance.dbgHelp.TrySymGetModuleBase64((long) (void*) moduleBase, out _) != HRESULT.S_OK)
            {
                var moduleInfo = Kernel32.GetModuleInformation(instance.dbgHelp.hProcess, (IntPtr) moduleBase);
                var fileName = Kernel32.GetModuleFileNameExW(instance.dbgHelp.hProcess, (IntPtr) moduleBase);

                instance.dbgHelp.SymLoadModuleEx(imageName: fileName, baseOfDll: moduleBase, dllSize: moduleInfo.SizeOfImage);
            }
        }

        private IDbgHelp dbgHelp;

        ~NativeReflector()
        {
            Log.Disable();
            dbgHelp.Dispose();
        }
    }
#endif
}
