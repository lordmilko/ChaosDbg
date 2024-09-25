using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.TTD;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    /// <summary>
    /// Provides facilities for loading and unloading symbols in a temporary <see cref="FastDbgHelp"/> instance as a TTD <see cref="Cursor"/> is replayed.
    /// </summary>
    class TtdSymbolManager : IDisposable
    {
        private SymbolProvider symbolProvider;

        private Cursor cursor;

        private HashSet<ModuleInstance> loadedModules = new HashSet<ModuleInstance>();

        public unsafe TtdSymbolManager(INativeLibraryProvider nativeLibraryProvider, ISymSrv symSrv, Cursor cursor)
        {
            symbolProvider = new SymbolProvider(nativeLibraryProvider, symSrv, new TtdCursorMemoryReader(cursor));

            this.cursor = cursor;
        }

        public unsafe void Update()
        {
            //Get the current list of loaded modules
            var activeModules = cursor.ModuleList.ToHashSet();

            //Unload any modules that are no longer loaded
            loadedModules.RemoveWhere(m =>
            {
                if (!activeModules.Contains(m))
                {
                    symbolProvider.UnloadModule(m.Module->address);
                    return true;
                }

                return false;
            });

            //Load any modules we're now missing
            foreach (var instance in activeModules)
            {
                if (!loadedModules.Contains(instance))
                {
                    //This is based on what DbgEng seems to send to DbgHelp
                    var fullPath = instance.Module->ToString();
                    var imageName = Path.GetFileName(fullPath);
                    var moduleName = Path.GetFileNameWithoutExtension(fullPath);

                    symbolProvider.LoadModule(imageName, instance.Module->address, (int) instance.Module->size);
                    loadedModules.Add(instance);
                }
            }
        }

        public unsafe string GetSymbol(CrossPlatformContext registerContext)
        {
            //This will include both native and module addressed
            if (symbolProvider.TryGetSymbolFromAddress(registerContext.IP, out var symbol))
            {
                if (symbol.Name == "memcpy")
                {
                    //Saying that a random memcpy was responsible for our value is not very helpful. Try and retrieve the calling function as well.
                    //There's no guarantee that the value returned from memcpy in rax will actually be the value that is used; e.g. in TTDReplay, QueryMemoryBuffer
                    //gets the memory value from a memcpy, but the pointer in rax is ignored; the value retrieved is still in rcx and is used from there
                    var returnAddress = (long) (void*) cursor.QueryMemoryBuffer<IntPtr>(registerContext.SP, QueryMemoryPolicy.Default);

                    if (symbolProvider.TryGetSymbolFromAddress(returnAddress, out var parentSymbol))
                    {
                        return $"{parentSymbol}-> {symbol}";
                    }
                }

                return symbol.ToString();
            }

            //No symbol, just use address
            return "0x" + registerContext.IP.ToString("X");
        }

        public void Dispose()
        {
            symbolProvider.Dispose();
        }
    }
}
