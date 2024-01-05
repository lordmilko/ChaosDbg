using ChaosDbg.DbgEng;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class DbgEngEngineTests : BaseTest
    {
        [TestMethod]
        public void DbgEngEngine_Launch_NativeOnPath()
        {
            var engineProvider = GetService<DbgEngEngineProvider>();

            using var engine = engineProvider.CreateProcess("notepad", true);
        }
    }
}
