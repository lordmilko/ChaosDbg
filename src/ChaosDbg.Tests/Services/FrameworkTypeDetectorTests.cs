using ChaosDbg.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class FrameworkTypeDetectorTests : BaseTest
    {
        [TestMethod]
        public void ExeTypeDetector_NetFramework()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void ExeTypeDetector_NetFramework_Mixed()
        {
            //PowerShell 5

            Test("C:\\Windows\\system32\\WindowsPowerShell\\v1.0\\powershell.exe", FrameworkKind.NetFramework);
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore_Mixed()
        {
            //PowerShell Core

            Test("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\"", FrameworkKind.NetCore);
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore_SingleFile()
        {
            //These are apparently a native wrapper that self extracts

            Assert.Inconclusive();
        }

        [TestMethod]
        public void ExeTypeDetector_NetFramework_Mixed_OnPath()
        {
            //PowerShell 5

            Test("powershell", FrameworkKind.NetFramework);
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore_Mixed_OnPath()
        {
            //We didn't specify a directory in our path, so Path.GetDirectoryName() will return null

            Assert.Inconclusive();
        }

        private void Test(string fileName, FrameworkKind expected)
        {
            var exeTypeDetector = GetService<IFrameworkTypeDetector>();

            var kind = exeTypeDetector.Detect(fileName);

            Assert.AreEqual(expected, kind);
        }
    }
}
