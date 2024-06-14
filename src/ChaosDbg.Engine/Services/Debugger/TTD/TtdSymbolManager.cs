using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosLib;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    /// <summary>
    /// Provides facilities for loading and unloading symbols in a temporary <see cref="FastDbgHelp"/> instance as a TTD <see cref="Cursor"/> is replayed.
    /// </summary>
    class TtdSymbolManager : IDisposable
    {
        private DbgHelpOptionsHolder originalOptions;
        private FastDbgHelp dbgHelp;

        private Cursor cursor;

        private HashSet<ModuleInstance> loadedModules = new HashSet<ModuleInstance>();

        public unsafe TtdSymbolManager(Cursor cursor)
        {
            originalOptions = new DbgHelpOptionsHolder();

            dbgHelp = new FastDbgHelp((IntPtr) 100);

            dbgHelp.SymInitialize(null, false);

            dbgHelp.GlobalOptions |= ClrDebug.DbgEng.SYMOPT.DEFERRED_LOADS;

            this.cursor = cursor;

            //In order to read the PE header information from the trace (you can't just assume that your DLLs on disk match those that were used in the trace)
            //we have to provide a read memory callback for DbgHelp to use

            dbgHelp.Callback.OnReadMemory = data =>
            {
                cursor.QueryMemoryBuffer(data->addr, data->buf, data->bytes, out var bytesRead, QueryMemoryPolicy.Default);
                *data->bytesread = (int) bytesRead;

                if (bytesRead == 0)
                    return false;

                //Not sure what I should return if we only manage to do a partial read
                Debug.Assert(data->bytes == bytesRead);

                return true;
            };
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
                    dbgHelp.SymUnloadModule64(m.Module->address);
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

                    dbgHelp.SymLoadModuleEx(imageName: imageName, moduleName: moduleName, baseOfDll: instance.Module->address, dllSize: (int) instance.Module->size);
                    loadedModules.Add(instance);
                }
            }
        }

        public unsafe string GetSymbol(CrossPlatformContext registerContext)
        {
            IMAGEHLP_MODULE64 moduleInfo;

            if (dbgHelp.TrySymFromAddr(registerContext.IP, out var symbol) == HRESULT.S_OK)
            {
                moduleInfo = dbgHelp.SymGetModuleInfo64(registerContext.IP);

                if (symbol.SymbolInfo.Name == "memcpy")
                {
                    //Saying that a random memcpy was responsible for our value is not very helpful. Try and retrieve the calling function as well.
                    //There's no guarantee that the value returned from memcpy in rax will actually be the value that is used; e.g. in TTDReplay, QueryMemoryBuffer
                    //gets the memory value from a memcpy, but the pointer in rax is ignored; the value retrieved is still in rcx and is used from there
                    var returnAddress = (long) (void*) cursor.QueryMemoryBuffer<IntPtr>(registerContext.SP, QueryMemoryPolicy.Default);

                    if (dbgHelp.TrySymFromAddr(returnAddress, out var parentSymbol) == HRESULT.S_OK)
                    {
                        var parentModuleInfo = dbgHelp.SymGetModuleInfo64(returnAddress);

                        return $"{parentModuleInfo.ModuleName}!{parentSymbol} -> {moduleInfo.ModuleName}!{symbol}";
                    }
                }

                return $"{moduleInfo.ModuleName}!{symbol}";
            }

            if (dbgHelp.TrySymGetModuleInfo64(registerContext.IP, out moduleInfo) == HRESULT.S_OK)
            {
                var rva = registerContext.IP - moduleInfo.BaseOfImage;

                return $"{moduleInfo.ModuleName}+0x{rva:X}";
            }

            return "0x" + registerContext.IP.ToString("X");
        }

        public void Dispose()
        {
            dbgHelp.Dispose();
            originalOptions.Dispose();
        }
    }
}
