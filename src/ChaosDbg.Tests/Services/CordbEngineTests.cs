using ChaosDbg.Cordb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class CordbEngineTests : BaseTest
    {
        [TestMethod]
        public void CordbEngine_Launch_FrameworkOnPath()
        {
            using var engine = GetService<CordbEngine>();

            engine.Launch("powershell", true);
        }
    }
}
