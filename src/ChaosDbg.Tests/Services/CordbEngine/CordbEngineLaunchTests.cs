using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public class CordbEngineLaunchTests : BaseTest
    {
        [TestMethod]
        public void CordbEngine_Launch_FrameworkOnPath()
        {
            var thread = new Thread(() =>
            {
                var cordebug = new CorDebug();
                var cb = new CorDebugManagedCallback();
                cordebug.SetManagedHandler(cb);

                cordebug.CreateProcess("powershell.exe");
            });

            thread.SetApartmentState(ApartmentState.MTA);

            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void CordbEngine_NetFramework_Create_WrongArchitecture()
        {
            var path = IntPtr.Size == 4
                ? "C:\\Windows\\sysnative\\WindowsPowerShell\\v1.0\\powershell.exe"
                : "C:\\Windows\\SysWOW64\\WindowsPowerShell\\v1.0\\powershell.exe";

            AssertEx.Throws<DebuggerInitializationException>(
                () => NetFrameworkProcess.Create(new CreateProcessOptions(path), null),
                "process most likely does not match architecture of debugger"
            );
        }

        [TestMethod]
        public void CordbEngine_NetCore_Create_WrongArchitecture()
        {
            AssertEx.Throws<DebuggerInitializationException>(
                () => NetCoreProcess.Create(new CreateProcessOptions(GetTestAppPath(matchCurrentProcess: false, netCore: true)), null),
                "however debugger process is"
            );
        }

        [TestMethod]
        public void CordbEngine_NetFramework_Attach_WrongArchitecture()
        {
            var path = IntPtr.Size == 4
                ? "C:\\Windows\\sysnative\\WindowsPowerShell\\v1.0\\powershell.exe"
                : "C:\\Windows\\SysWOW64\\WindowsPowerShell\\v1.0\\powershell.exe";

            var psi = new ProcessStartInfo(path)
            {
                WindowStyle = ProcessWindowStyle.Minimized
            };
            var process = Process.Start(psi);

            try
            {
                AssertEx.Throws<DebuggerInitializationException>(
                    () => NetFrameworkProcess.Attach(new AttachProcessOptions(process.Id), null),
                    "process most likely does not match architecture of debugger"
                );
            }
            finally
            {
                process.Kill();
            }
        }

        [TestMethod]
        public void CordbEngine_NetCore_Attach_WrongArchitecture()
        {
            var path = GetTestAppPath(matchCurrentProcess: false, netCore: true);

            var psi = new ProcessStartInfo(path, EventName)
            {
                WindowStyle = ProcessWindowStyle.Minimized
            };

            var process = Process.Start(psi);

            try
            {
                AssertEx.Throws<DebuggerInitializationException>(
                    () => NetCoreProcess.Attach(new AttachProcessOptions(process.Id), null),
                    "however debugger process is"
                );
            }
            finally
            {
                process.Kill();
            }
        }
    }
}
