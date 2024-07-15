using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.DbgEng
{
    public class DbgEngMemoryReader : MemoryReader
    {
        private DbgEngProcess process;

        public DbgEngMemoryReader(DbgEngProcess process) : base(process.Is32Bit)
        {
            this.process = process;
        }

        public override HRESULT ReadVirtual(long address, IntPtr buffer, int bytesRequested, out int bytesRead) =>
            process.Session.EngineClient.DataSpaces.TryReadVirtual(address, buffer, bytesRequested, out bytesRead);
    }
}
