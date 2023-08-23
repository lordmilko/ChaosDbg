using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ChaosDbg.Tests
{
    public static class NativeMethods
    {
        private const string ole32 = "ole32.dll";

        #region ole32.dll

        [DllImport(ole32, SetLastError = true)]
        public static extern int CoRegisterMessageFilter(IMessageFilter messageFilter, out IMessageFilter oldMessageFilter);

        [DllImport(ole32, PreserveSig = false)]
        public static extern void CreateBindCtx(int reserved, [MarshalAs(UnmanagedType.Interface)] out IBindCtx bindContext);

        [DllImport(ole32, PreserveSig = false)]
        public static extern void GetRunningObjectTable(int reserved, [MarshalAs(UnmanagedType.Interface)] out IRunningObjectTable runningObjectTable);

        #endregion
    }
}
