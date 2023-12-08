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
    public class CordbEngineTests : BaseTest
    {
        public TestContext TestContext { get; set; }

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

        #region StackTrace

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_ManagedFrames()
        {
            TestCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                engine =>
                {
                    var thread = engine.ActiveProcess.Threads.Single();

                    thread.Verify().StackTrace(
                        "System.Threading.Thread.Sleep",
                        "TestApp.Program.SignalReady",
                        "TestApp.CordbEngine_Thread_StackTrace.Managed",
                        "TestApp.Program.Main"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_InternalFrames()
        {
            TestCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                engine =>
                {
                    var thread = engine.ActiveProcess.Threads.Single();

                    thread.Verify().StackTrace(
                        "System.Threading.Thread.Sleep",
                        "TestApp.Program.SignalReady",
                        "<>c.<Internal>b__1_0",
                        "",
                        "",
                        "TestApp.CordbEngine_Thread_StackTrace.Managed",
                        "TestApp.Program.Main"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_NativeFrames()
        {
            TestCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                engine =>
                {
                    var cordbFrames = engine.ActiveProcess.Threads.Single().StackTrace;
                    var dbgEngFrames = GetDbgEngFrames(engine);

                    Assert.AreEqual(cordbFrames.Length, dbgEngFrames.Length);

                    for (var i = 0; i < cordbFrames.Length; i++)
                    {
                        var cdbFrame = cordbFrames[i];
                        var deFrame = dbgEngFrames[i];

                        if (cdbFrame is CordbILFrame f)
                        {
                            Assert.AreEqual(deFrame.IP, f.Context.IP);
                            Assert.AreEqual(deFrame.SP, f.Context.SP);

                            //DbgEng's frames don't store the BP so we can't validate thats
                        }
                        else
                            Assert.AreEqual(cdbFrame.ToString(), deFrame.ToString());
                    }
                },
                useInterop: true
            );
        }

        private DbgEngFrame[] GetDbgEngFrames(CordbEngine engine)
        {
            using var dbgEngEngine = GetService<DbgEngEngine>();

            dbgEngEngine.Attach(engine.ActiveProcess.Id, true);
            dbgEngEngine.WaitForBreak();

            var frames = dbgEngEngine.GetStackTrace();

            return frames;
        }

        #endregion

        private string EventName => $"ChaosDbg_Test_{Process.GetCurrentProcess().Id}_{TestContext.TestName}";

        private void TestCreate(
            TestType testType,
            Action<CordbEngine> action,
            bool matchCurrentProcess = true,
            bool netCore = false,
            bool useInterop = false)
        {
            using var engine = (CordbEngine) GetService<ICordbEngine>();

            var path = GetTestAppPath(matchCurrentProcess, netCore);

            Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());

            engine.CreateProcess($"\"{path}\" {testType} {EventName}", useInterop: useInterop);

            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

            eventHandle.WaitOne();

            engine.Break();

            action(engine);
        }

        private string GetTestAppPath(bool matchCurrentProcess, bool netCore)
        {
            var dllPath = GetType().Assembly.Location;

            var suffix = matchCurrentProcess ?
                IntPtr.Size == 4 ? "x86" : "x64" :
                IntPtr.Size == 4 ? "x64" : "x86";

#if DEBUG
            var configuration = "Debug";
#else
            var configuration = "Release";
#endif

            var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dllPath), "..", "..", "..", $"TestApp.{suffix}", "bin", configuration, netCore ? "net5.0" : "net472", $"TestApp.{suffix}.exe"));

            return dir;
        }
    }
}
