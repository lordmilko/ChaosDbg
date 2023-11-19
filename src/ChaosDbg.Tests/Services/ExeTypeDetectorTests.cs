using ChaosDbg.Engine;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class ExeTypeDetectorTests
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

            Test("C:\\Windows\\system32\\WindowsPowerShell\\v1.0\\powershell.exe", ExeKind.NetFramework);
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore_Mixed()
        {
            //PowerShell Core

            Test("C:\\Program Files\\PowerShell\\7\\pwsh.exe", ExeKind.NetCore);
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

            Test("powershell", ExeKind.NetFramework);
        }

        [TestMethod]
        public void ExeTypeDetector_NetCore_Mixed_OnPath()
        {
            //We didn't specify a directory in our path, so Path.GetDirectoryName() will return null

            Assert.Inconclusive();
        }

        private void Test(string fileName, ExeKind expected)
        {
            var serviceCollection = new ServiceCollection
            {
                { typeof(IExeTypeDetector), typeof(ExeTypeDetector) },
                { typeof(IPEFileProvider), typeof(PEFileProvider) },
                { typeof(ISigReader), typeof(SigReader) }
            };

            var serviceProvider = serviceCollection.Build();

            var exeTypeDetector = serviceProvider.GetService<IExeTypeDetector>();

            var kind = exeTypeDetector.Detect(fileName);

            Assert.AreEqual(expected, kind);
        }
    }
}
