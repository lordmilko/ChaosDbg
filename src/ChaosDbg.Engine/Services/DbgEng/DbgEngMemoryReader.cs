using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.DbgEng
{
    public class DbgEngMemoryReader : IMemoryReader
    {
        public bool Is32Bit => process.Is32Bit;
        public int PointerSize => Is32Bit ? 4 : 8;

        private DbgEngProcess process;

        public DbgEngMemoryReader(DbgEngProcess process)
        {
            this.process = process;
        }

        public HRESULT ReadVirtual(long address, IntPtr buffer, int bytesRequested, out int bytesRead) =>
            process.Session.EngineClient.DataSpaces.TryReadVirtual(address, buffer, bytesRequested, out bytesRead);
    }
}
