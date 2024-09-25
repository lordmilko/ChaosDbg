using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ChaosLib;
using ChaosLib.Detour;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;
using ChaosLib.TypedData;
using ClrDebug;
using ClrDebug.DIA;

namespace ChaosDbg
{
#if DEBUG
    /// <summary>
    /// Provides facilities for interacting with native symbols that have been loaded into ChaosDbg itself.
    /// </summary>
    class NativeReflector : IDisposable
    {
        private static NativeReflector instanceRaw;
        private static object getOrAddSymbolLock = new();

        private SymbolProvider symbolProvider;
        private ITypedDataAccessor accessor;
        private bool disposed;

        private static NativeReflector instance
        {
            get
            {
                Debug.Assert(instanceRaw != null, $"{nameof(NativeReflector)} has not been initialized");
                return instanceRaw;
            }
        }

        public NativeReflector(INativeLibraryProvider nativeLibraryProvider, ISymSrv symSrv)
        {
            symbolProvider = new SymbolProvider(nativeLibraryProvider, symSrv, new LiveProcessMemoryReader(Kernel32.GetCurrentProcess()));
            accessor = new LiveProcessTypedDataAccessor(Kernel32.GetCurrentProcess(), symbolProvider);
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            var nativeReflector = serviceProvider.GetService<NativeReflector>();

            Initialize(nativeReflector);
        }

        public static void Initialize(NativeReflector nativeReflector)
        {
            instanceRaw = nativeReflector;
        }

        public static ObjectTypedValue GetTypedData<T>(ComObject<T> comObject) => GetTypedData(comObject.Raw);

        public static unsafe ObjectTypedValue GetTypedData(object rcw)
        {
            instance.ThrowIfDisposed();

            //The functionality of this method is highly specific to the current process (i.e. it being an RCW), thus is defined entirely within NativeReflector

            if (rcw == null)
                throw new ArgumentNullException(nameof(rcw));

            if (rcw.GetType().ToString() != "System.__ComObject")
                throw new ArgumentException($"Object '{rcw}' is not of type 'System.__ComObject'");

            /* When you expand a COM object in Visual Studio, it's possible to see a Native View, that (when you have the relevant symbols) will magically show the original object
             * that this VTable pointer belongs to. How does this work?
             *
             * Having spent quite a large amount of time on this issue, here's what I can say:
             *
             * Suppose you have an ICorDebug interface, and want to retrieve the original mscordbi!Cordb that it belongs to. Marshal.GetIUnknownForObject() will give us a _pointer to the VTable_ of this object.
             * So to get the address of the VTable symbol itself, we must do *(IntPtr*) Marshal.GetIUnknownForObject().
             *
             * When we do SymFromAddr on this address, we'll get back a Cordb::`vftable' symbol. But what is this? In the Cordb::Cordb() ctor, the address of several VTable objects in the .rdata section are stored
             * in various members of the Cordb object instance (e.g. Cordb->ICorDebug = &vtable). The tricky part here is that there is no symbol information that strictly ties a given VTable definition to
             * a C++ object that uses it. Complicating matters further, there's actually _two_ symbols that exist for a given VTable address
             *
             * - If you try and resolve the VTable address from the global scope, you'll get back a Cordb::`vftable' which is part of an ArrayType that says how big the VTable is, but doesn't seem to provide any way of
             *   actually linking the VTable back to a particular C++ class
             * - If you try and resolve the VTable from the EXE's scope, you'll get back a Cordb::`vftable'{for `ICorDebug'} object. This symbol is a bit more helpful, in that it actually lists the VTable that its a part of.
             *   Once again however, no information is provided about which C++ class this links back to
             *
             * Based on this, it seems like the only option we seem to have is to try and match the VTable back to the C++ object by parsing its name. We can clearly see that Cordb::`vtable' partains to the Cordb class, and
             * we can also see that Cordb::`vftable'{for `ICorDebug'} relates to the ICorDebug interface on that class. There is no way to extract the base type from an undecorated name via any combination of UNDNAME values however,
             * so our only option for extracting the base type would be to use a regular expression.
             *
             * As an example:
             *
             * The layout of the Cordb object is as follows
             *
             * Cordb
             * | ---------------------- |
             * | CordbBase           0) |
             * | ---------------------- |             Cordb::`vftable'{for `ICorDebug'}
             * | ICorDebug       (0x28) |  -------->  | -------------- |
             * | ---------------------- |             | QueryInterface |
             * | ICorDebugRemote (0x30) |             | AddRef         |
             * | ---------------------- |             | ...            |
             * | <normal fields>        |
             * | ---------------------- |
             *
             * When the Cordb object is initialized, I believe the ICorDebug field is assigned a _copy_ of the VTable definition contained in the .rdata. When
             * you do Cordb::QueryInterface, I think you are handed the value of &this->ICorDebug? This doesn't sound right, but the logic below
             * seems to indicate this is what happens...but wouldn't that mean it's a pointer to a pointer to the VTable then? I'm a bit confused!
             */

            //This gives us a _pointer to_ the vtable
            var pUnk = (long) (void*) Marshal.GetIUnknownForObject(rcw);

            //This gives us the address of the vtable itself
            IntPtr vtableAddr = *(IntPtr*) pUnk;

            //We can't just do SymFromAddr, as this will give us a data symbol for the definition of the vtable within the .rdata section.
            //What we need is a public symbol which will contain the offset of this vtable within its parent class

            long moduleBase;

            ISymbolModule symbolModule;

            lock (getOrAddSymbolLock)
            {
                if (!instance.symbolProvider.TryGetSymbolModuleForAddress((long) (void*) vtableAddr, out symbolModule))
                {
                    moduleBase = (long) (void*) Kernel32.VirtualQuery((IntPtr) vtableAddr).AllocationBase;

                    var moduleInfo = Kernel32.GetModuleInformation(Kernel32.GetCurrentProcess(), (IntPtr) moduleBase);
                    var moduleName = Kernel32.GetModuleFileNameExW(Kernel32.GetCurrentProcess(), (IntPtr) moduleBase);

                    symbolModule = instance.symbolProvider.LoadModule(moduleName, moduleBase, moduleInfo.SizeOfImage).Result;
                }
            }

            var diaSession = ((MicrosoftPdbSymbolModule) symbolModule).DiaSession;

            var rva = (int) ((long) (void*) vtableAddr - symbolModule.Address);
            var vtableSymbol = diaSession.FindSymbolByRVA(rva, SymTagEnum.PublicSymbol);

            var undecoratedName = vtableSymbol.GetUndecoratedNameEx(UNDNAME.UNDNAME_COMPLETE);

            //There isn't an UNDNAME we can use to extract the base class. So beyond implementing our own demangler (which will be too complex to be maintainable) our best option is to just try and extract the info we need via regex
            var match = Regex.Match(undecoratedName, "(.+?)::`vftable'{for `(.+?)'}");

            if (!match.Success)
                throw new InvalidOperationException($"Failed to parse VTable name '{undecoratedName}'");

            var className = match.Groups[1].Value;

            var interfaceName = match.Groups[2].Value;

            if (className.StartsWith("const "))
                className = className.Substring(6);
            //This will load the module if it isn't loaded
            return instance.dbgHelp.TypedDataProvider.CreateObjectForRCW(comObject.Raw);
        }

        public static void InstallThunkAwareHook<TDelegate, THook>(string pattern, THook hook)
            where TDelegate : Delegate
            where THook : Delegate
        {
            instance.ThrowIfDisposed();

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
            instance.ThrowIfDisposed();

            EnsureModuleLoaded(pattern, out _, out _, out _);

            if (!instance.symbolProvider.TryGetSymbolFromName(pattern, out var symbol))
                throw new InvalidOperationException($"Could not find any symbols that match the pattern '{pattern}'");

            var original = Marshal.GetDelegateForFunctionPointer<TDelegate>((IntPtr) symbol.Address);

            DetourBuilder.AddSymbolHook(original, hook);
        }

        public static unsafe IDbgRemoteField[] GetGlobals(string pattern)
        {
            instance.ThrowIfDisposed();

            EnsureModuleLoaded(pattern, out var moduleBase, out _, out var symbolName);

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
            instance.ThrowIfDisposed();

            EnsureModuleLoaded(pattern, out _, out _, out _);

            var symbol = instance.symbolProvider.GetSymbolFromName(pattern);

            var result = Marshal.PtrToStructure<T>((IntPtr) symbol.Address);

            return result;
        }

        public static T GetGlobal<T>(IntPtr address)
        {
            instance.ThrowIfDisposed();

            var result = Marshal.PtrToStructure<T>(address);

            return result;
        }

        private static unsafe void EnsureModuleLoaded(string pattern, out long moduleBase, out string moduleName, out string symbolName)
        {
            var exclamation = pattern.IndexOf('!');

            if (exclamation <= 0)
                throw new ArgumentException("Pattern '{pattern}' is invalid: could not find a '!' delimiting the module and symbol name", nameof(pattern));

            moduleName = pattern.Substring(0, exclamation);
            symbolName = pattern.Substring(exclamation + 1);

            moduleBase = (long) (void*) Kernel32.GetModuleHandleW(moduleName);

            lock (getOrAddSymbolLock)
            {
                if (!instance.symbolProvider.TryGetSymbolModuleForAddress((long) (void*) moduleBase, out _))
                {
                    var moduleInfo = Kernel32.GetModuleInformation(Kernel32.GetCurrentProcess(), (IntPtr) moduleBase);
                    var fileName = Kernel32.GetModuleFileNameExW(Kernel32.GetCurrentProcess(), (IntPtr) moduleBase);

                    instance.symbolProvider.LoadModule(imageName: fileName, moduleBase, moduleInfo.SizeOfImage);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NativeReflector));
        }

        public void Dispose()
        {
            symbolProvider.Dispose();
            disposed = true;
        }
    }
#endif
}
