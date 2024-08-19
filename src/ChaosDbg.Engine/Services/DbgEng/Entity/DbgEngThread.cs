using System;
using System.Diagnostics;

namespace ChaosDbg.DbgEng
{
    [DebuggerDisplay("[{UserId}] TID = {SystemId}, PID = {Process.Id}")]
    public class DbgEngThread : IDbgThread
    {
        public DbgEngProcess Process { get; }

        int IDbgThread.Id => SystemId;

        public int SystemId { get; }

        public int UserId { get; }

        public IntPtr Handle { get; }

        public RemoteTeb Teb { get; }

        public DbgEngThread(int userId, int systemId, long handle, long tebAddress, DbgEngProcess process)
        {
            //Kernel32 owns this thread, not us. So we don't need to dispose it. It will be closed
            //after the EXIT_PROCESS_DEBUG_EVENT has been processed
            Handle = (IntPtr) handle;
            Teb = RemoteTeb.FromTeb(tebAddress, process.MemoryReader);
            UserId = userId;
            SystemId = systemId;
            Process = process;
        }
    }
}
