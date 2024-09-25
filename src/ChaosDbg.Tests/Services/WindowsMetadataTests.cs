using ChaosLib.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unit = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class WindowsMetadataTests
    {
        private static WindowsMetadataProvider provider;

        [ClassInitialize]
        public static void ClassInitialize(Unit.TestContext context)
        {
            provider = new WindowsMetadataProvider();
        }

        [TestMethod]
        public void WinMD_GetFunction()
        {
            var function = provider.GetFunction("CreateFileW");

            Assert.AreEqual("HANDLE CreateFileW(PWSTR lpFileName, uint dwDesiredAccess, FILE_SHARE_MODE dwShareMode, SECURITY_ATTRIBUTES* lpSecurityAttributes, FILE_CREATION_DISPOSITION dwCreationDisposition, FILE_FLAGS_AND_ATTRIBUTES dwFlagsAndAttributes, HANDLE hTemplateFile)", function.ToString());
        }

        [TestMethod]
        public void WinMD_GetMethod_IgnoreCase()
        {
            var function = provider.GetFunction("createfilew");

            Assert.AreEqual("HANDLE CreateFileW(PWSTR lpFileName, uint dwDesiredAccess, FILE_SHARE_MODE dwShareMode, SECURITY_ATTRIBUTES* lpSecurityAttributes, FILE_CREATION_DISPOSITION dwCreationDisposition, FILE_FLAGS_AND_ATTRIBUTES dwFlagsAndAttributes, HANDLE hTemplateFile)", function.ToString());
        }

        [TestMethod]
        public void WinMD_GetConstant_Number()
        {
            var function = provider.GetConstant("TXTLOG_INFDB");

            Assert.AreEqual("uint TXTLOG_INFDB = 1024", function.ToString());
        }

        [TestMethod]
        public void WinMD_GetConstant_String()
        {
            var constant = provider.GetConstant("INSTALLPROPERTY_PACKAGENAME");

            Assert.AreEqual("string INSTALLPROPERTY_PACKAGENAME = \"PackageName\"", constant.ToString());
        }

        [TestMethod]
        public void WinMD_GetConstant_TransparentType()
        {
            var constant = provider.GetConstant("MSIDBOPEN_TRANSACT");

            Assert.AreEqual("PWSTR MSIDBOPEN_TRANSACT = 1", constant.ToString());
        }

        [TestMethod]
        public void WinMD_GetConstant_IgnoreCase()
        {
            var constant = provider.GetConstant("msidbopen_transact");

            Assert.AreEqual("PWSTR MSIDBOPEN_TRANSACT = 1", constant.ToString());
        }
    }
}
