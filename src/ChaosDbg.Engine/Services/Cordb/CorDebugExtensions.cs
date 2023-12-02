using System.Runtime.InteropServices;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    static class CorDebugExtensions
    {
        public static string ReadUnicode(this ICLRDataTarget dataTarget, CLRDATA_ADDRESS address, int size)
        {
            var buffer = Marshal.AllocHGlobal(size);

            try
            {
                dataTarget.ReadVirtual(address, buffer, size, out var bytesRead).ThrowOnNotOK();

                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
