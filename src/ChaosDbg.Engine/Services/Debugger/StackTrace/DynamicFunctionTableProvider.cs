using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChaosDbg.Cordb;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ClrDebug;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for retrieving dynamic function tables which are required
    /// to properly unwind call stacks in 64-bit versions of Windows.
    /// </summary>
    class DynamicFunctionTableProvider : IDisposable
    {
        /* Typically, 64-bit functions have all the information required to unwind them
         * baked into their assemblies. However when it comes to dynamically generated functions,
         * such as managed frames, a native stack unwinder requires extra assistance in order to
         * unwind these frames properly. Without this assistance, the return address of each frame
         * will immediately start spiralling into incoherence. */

        private ICLRDataTarget dataTarget;
        private DynamicFunctionTableCache dynamicFunctionTableCache;
        private Dictionary<string, (IntPtr hModule, POUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK callback)> loadedLibraries = new Dictionary<string, (IntPtr hModule, POUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK callback)>();
        private bool disposed;

        public DynamicFunctionTableProvider(ICLRDataTarget dataTarget, DynamicFunctionTableCache dynamicFunctionTableCache)
        {
            this.dataTarget = dataTarget;
            this.dynamicFunctionTableCache = dynamicFunctionTableCache;
        }

        public bool TryGetDynamicFunctionEntry(IntPtr hProcess, long address, out RUNTIME_FUNCTION match)
        {
            match = default;

            //ntdll is loaded into the same address in every process. As such,
            //the dynamic function table is also 
            var headAddr = Ntdll.Native.RtlGetFunctionTableListHead();

            //Load the LIST_ENTRY from the target process
            var head = dataTarget.ReadVirtual<LIST_ENTRY>(headAddr);

            var current = head.Flink;

            if (current == IntPtr.Zero)
                return false;

            while (current != headAddr)
            {
                var table = dataTarget.ReadVirtual<AMD64_DYNAMIC_FUNCTION_TABLE>(current);

                if (table.Type == AMD64_FUNCTION_TABLE_TYPE.AMD64_RF_CALLBACK)
                {
                    //To get the entries in this function table we need to interact with a special DLL
                    //that is provided by the target process. In the case of managed processes, this is
                    //mscordacwks
                    if (TryGetCallbackDllFunctionEntry(hProcess, table, current, address, out match))
                        return true;
                }
                else
                {
                    //It's a "normal" dynamic function
                    if (TryGetNormalFunctionEntry(table, address, out match))
                        return true;
                }

                //Move to the next table
                current = table.ListEntry.Flink;
            }

            return false;
        }

        public bool TryGetDynamicFunctionTableModuleBase(long address, out long baseAddress)
        {
            baseAddress = default;

            //ntdll is loaded into the same address in every process. As such,
            //the dynamic function table is also 
            var headAddr = Ntdll.Native.RtlGetFunctionTableListHead();

            //Load the LIST_ENTRY from the target process
            var head = dataTarget.ReadVirtual<LIST_ENTRY>(headAddr);

            var current = head.Flink;

            if (current == IntPtr.Zero)
                return false;

            while (current != headAddr)
            {
                var table = dataTarget.ReadVirtual<AMD64_DYNAMIC_FUNCTION_TABLE>(current);

                if (address >= table.MinimumAddress && address < table.MaximumAddress)
                {
                    baseAddress = table.BaseAddress;
                    return true;
                }

                //Move to the next table
                current = table.ListEntry.Flink;
            }

            return false;
        }

        private unsafe bool TryGetCallbackDllFunctionEntry(
            IntPtr hProcess,
            in AMD64_DYNAMIC_FUNCTION_TABLE table,
            IntPtr tablePtr,
            long targetAddress,
            out RUNTIME_FUNCTION match)
        {
            match = default;

            var outOfProcessDll = dataTarget.ReadUnicode(table.OutOfProcessCallbackDll, 260 * 2);

            if (targetAddress > table.MinimumAddress && targetAddress < table.MaximumAddress && outOfProcessDll != null)
            {
                if (!loadedLibraries.TryGetValue(outOfProcessDll, out var existing))
                {
                    var hModule = Kernel32.LoadLibrary(outOfProcessDll);

                    var pCallback = Kernel32.GetProcAddress(hModule, Ntdll.OUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK_EXPORT_NAME);
                    var callback = Marshal.GetDelegateForFunctionPointer<POUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK>(pCallback);

                    existing = (hModule, callback);

                    loadedLibraries[outOfProcessDll] = existing;
                }

                if (!dynamicFunctionTableCache.TryGetOrAdd(hProcess, existing.hModule, existing.callback, tablePtr, out var functionEntries))
                    return false;

                return TrySearchFunctionEntries(table, functionEntries, targetAddress, out match);
            }

            return false;
        }

        private unsafe bool TryGetNormalFunctionEntry(
            in AMD64_DYNAMIC_FUNCTION_TABLE table,
            long targetAddress,
            out RUNTIME_FUNCTION match)
        {
            //Just read the entries straight from the target process

            var bufferSize = table.EntryCount * Marshal.SizeOf<RUNTIME_FUNCTION>();

            using var buffer = new MemoryBuffer(bufferSize);

            dataTarget.ReadVirtual(table.FunctionTable, buffer, bufferSize, out _).ThrowOnNotOK();

            var pArray = (RUNTIME_FUNCTION*) (IntPtr) buffer;

            var functionEntries = new RUNTIME_FUNCTION[table.EntryCount];

            for (var i = 0; i < table.EntryCount; i++, pArray++)
            {
                functionEntries[i] = *pArray;
            }

            return TrySearchFunctionEntries(table, functionEntries, targetAddress, out match);
        }

        private bool TrySearchFunctionEntries(
            in AMD64_DYNAMIC_FUNCTION_TABLE table,
            RUNTIME_FUNCTION[] functionEntries,
            long targetAddress,
            out RUNTIME_FUNCTION match)
        {
            foreach (var item in functionEntries)
            {
                var startAddress = table.BaseAddress + item.BeginAddress;
                var endAddress = table.BaseAddress + item.EndAddress;

                if (targetAddress > startAddress && targetAddress < endAddress)
                {
                    match = item;
                    return true;
                }
            }

            match = default;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            foreach (var value in loadedLibraries.Values)
                Kernel32.FreeLibrary(value.hModule);

            loadedLibraries.Clear();

            disposed = false;
        }
    }
}
