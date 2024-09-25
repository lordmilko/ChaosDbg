using System.Threading;
using ChaosDbg.SymStore;

namespace ChaosDbg.Tests
{
    public static class WellKnownTestModule
    {
        public static SymbolStoreKey Ntdll = new SymbolStoreKey("ntdll.dll/BCED4B82217000/ntdll.dll", "C:\\windows\\system32\\ntdll.dll");

        public static string CreateKey(string modulePath)
        {
            var symbolClient = new SymbolClient(new NullSymStoreLogger());

            var key = symbolClient.GetKey(modulePath);

            return $"new {nameof(SymbolStoreKey)}(\"{key.Index}\", \"{key.FullPathName.Replace("\\", "\\\\")}\")";
        }

        public static string GetStoreFile(SymbolStoreKey key)
        {
            var symbolClient = new SymbolClient(new NullSymStoreLogger());

            var path = symbolClient.GetStoreFile(key, CancellationToken.None);

            return path;
        }

        public static string GetIDALst(string storeFile) =>
            IDAProvider.GetLst(storeFile);
    }
}
