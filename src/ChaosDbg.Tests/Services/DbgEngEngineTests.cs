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
            using var engine = GetService<DbgEngEngine>();

            engine.Launch("notepad", true);
        }
    }
}
