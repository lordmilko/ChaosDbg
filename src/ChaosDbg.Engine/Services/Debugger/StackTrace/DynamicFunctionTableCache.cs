using System;
using System.Collections.Generic;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ClrDebug;

namespace ChaosDbg
{
    /// <summary>
    /// Caches dynamic function table entries across debugger break-ins.
    /// </summary>
    class DynamicFunctionTableCache
    {
        /* We need to get RUNTIME_FUNCTION entries in order to show x64 stack traces properly. While unwinding a given stack,
         * we may need to query this information hundreds of times, returning the exact same couple of table lists each time.
         * Each function table can have potentially thousands of RUNTIME_FUNCTION entries in it, making it very inefficient
         * to keep having our OUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK keep regenerating this information over and over and over.
         * Thus, we cache this information, clearing it each time the debuggee resumes. This is important because as new methods
         * are jitted, they may be added to the same few function tables we already cached, making this data stale. */

        private Dictionary<(IntPtr, IntPtr, IntPtr), RUNTIME_FUNCTION[]> cache = new Dictionary<(IntPtr, IntPtr, IntPtr), RUNTIME_FUNCTION[]>();

        internal unsafe bool TryGetOrAdd(
            IntPtr hProcess,
            IntPtr hModule,
            POUT_OF_PROCESS_FUNCTION_TABLE_CALLBACK callback,
            IntPtr tablePtr,
            out RUNTIME_FUNCTION[] functionEntries)
        {
            if (!cache.TryGetValue((hProcess, hModule, tablePtr), out functionEntries))
            {
                //Don't have this table, so invoke the callback
                var status = callback(hProcess, tablePtr, out var entries, out var pFunctions);

                //Not sure whether I should cache anything on fail so we don't ask again. For now, we don't cache
                if (status != NTSTATUS.STATUS_SUCCESS)
                    return false;

                if ((IntPtr) pFunctions == IntPtr.Zero)
                    return false;

                var original = pFunctions;

                try
                {
                    functionEntries = new RUNTIME_FUNCTION[entries];

                    for (var i = 0; i < entries; i++, pFunctions++)
                        functionEntries[i] = *pFunctions;

                    cache[(hProcess, hModule, tablePtr)] = functionEntries;

                    return true;
                }
                finally
                {
                    //DbgEng calls RtlFreeHeap(RtlProcessHeap()). RtlProcessHeap is defined as NtCurrentPeb()->ProcessHeap, which we can't access in C#.
                    //kernel32!GetProcessHeap seems to do the same thing however: HeapFree ultimately defers to RtlpFreeHeapInternal and when you look
                    //at how mscordacwks allocates its buffer, it uses HeapAlloc, which means whatever it's doing must be the normal rules that all
                    //out of proc callback DLLs must follow
                    Kernel32.HeapFree(Kernel32.GetProcessHeap(), 0, (IntPtr) original);
                }
            }

            return true;
        }

        public void Clear() => cache.Clear();
    }
}
