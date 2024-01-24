using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a stream capable of reading the memory of a remote processes address space via kernel32!ReadProcessMemory
    /// </summary>
    class ProcessMemoryStream : RemoteMemoryStream
    {
        private readonly MemoryReader memoryReader;

        public ProcessMemoryStream(IntPtr hProcess)
        {
            memoryReader = new MemoryReader(hProcess);
        }

        protected override HRESULT ReadVirtual(long address, int count, out byte[] result) =>
            memoryReader.TryReadVirtual(address, count, out result);
    }
}
