using System;
using System.Diagnostics;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.Metadata;
using ChaosLib;
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
                CordbLauncher.LogNativeCorDebugInfo(cordebug);
                var cb = new CorDebugManagedCallback();

                var exited = new ManualResetEventSlim(false);
                cb.OnDebuggerError += (s, e) => exited.Set();
                cb.OnExitProcess += (s, e) => exited.Set();
                cordebug.SetManagedHandler(cb);

                var process = cordebug.CreateProcess("powershell.exe");
                CordbLauncher.LogNativeCorDebugProcessInfo(process);

                process.Stop(0);
                process.Terminate(0);

                exited.Wait();

                cordebug.Terminate();
            });
            Log.CopyContextTo(thread);

            thread.SetApartmentState(ApartmentState.MTA);

            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void CordbEngine_Launch_Crash_AfterStartup_OnManagedEventThread_ManagedOnly()
        {
            void DoTest()
            {
                TestSignalledDebugCreate(
                    TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                    action: ctx => ctx.WaitForFatalShutdown(),
                    hookEvents: provider =>
                    {
                        provider.ModuleLoad += (s, e) => throw new DummyException();
                    },
                    waitForSignal: false
                );
            }

            AssertEx.Throws<DummyException>(DoTest, "DummyException");
        }

        [TestMethod]
        public void CordbEngine_Launch_Crash_AfterStartup_OnManagedEventThread_Interop()
        {
            void DoTest()
            {
                TestSignalledDebugCreate(
                    TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                    action: ctx => ctx.WaitForFatalShutdown(),
                    hookEvents: provider =>
                    {
                        provider.ModuleLoad += (s, e) =>
                        {
                            if (e.Module is not CordbNativeModule)
                                throw new DummyException();
                        };
                    },
                    useInterop: true,
                    handleLoaderBP: true,
                    waitForSignal: false
                );
            }

            AssertEx.Throws<DummyException>(DoTest, "DummyException");
        }

        [TestMethod]
        public void CordbEngine_Launch_Crash_AfterStartup_OnUnmanagedEventThread_OutOfBand_Interop() =>
            TestUnmanagedEventThreadCrash(crashInOutOfBand: true);

        private void TestUnmanagedEventThreadCrash(bool crashInOutOfBand)
        {
            void DoTest()
            {
                TestSignalledDebugCreate(
                    TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                    action: ctx => ctx.WaitForFatalShutdown(),
                    hookEvents: provider =>
                    {
                        provider.ModuleLoad += (s, e) =>
                        {
                            if (e.Module is CordbNativeModule m)
                            {
                                var isOutOfBand = m.Process.Session.CallbackContext.UnmanagedOutOfBand;

                                if (crashInOutOfBand)
                                {
                                    if (isOutOfBand)
                                        throw new DummyException();
                                }
                                else
                                {
                                    if (!isOutOfBand)
                                        throw new DummyException();
                                }
                            }
                        };
                    },
                    useInterop: true,
                    handleLoaderBP: true,
                    waitForSignal: false
                );
            }

            AssertEx.Throws<DummyException>(DoTest, "DummyException");
        }

        [TestMethod]
        public void CordbEngine_NetFramework_Create_WrongArchitecture()
        {
            var path = IntPtr.Size == 4
                ? "C:\\Windows\\sysnative\\WindowsPowerShell\\v1.0\\powershell.exe"
                : "C:\\Windows\\SysWOW64\\WindowsPowerShell\\v1.0\\powershell.exe";

            AssertEx.Throws<DebuggerInitializationException>(
                () => CordbLauncher.Create(new CreateProcessTargetOptions(path) { FrameworkKind = FrameworkKind.NetCore }, null, default),
                "however debugger process is"
            );
        }

        [TestMethod]
        public void CordbEngine_NetCore_Create_WrongArchitecture()
        {
            AssertEx.Throws<DebuggerInitializationException>(
                () => CordbLauncher.Create(new CreateProcessTargetOptions(GetTestAppPath(matchCurrentProcess: false, netCore: true)) { FrameworkKind = FrameworkKind.NetCore }, null, default),
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
                    () => CordbLauncher.Attach(new AttachProcessTargetOptions(process.Id) { FrameworkKind = FrameworkKind.NetFramework }, null, default),
                    "however debugger process is"
                );
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill();
            }
        }

        [TestMethod]
        public void CordbEngine_NetCore_Attach_WrongArchitecture()
        {
            var path = GetTestAppPath(matchCurrentProcess: false, netCore: true);

            var psi = new ProcessStartInfo(path, $"{TestType.CordbEngine_Thread_StackTrace_ManagedFrames} {EventName}")
            {
                WindowStyle = ProcessWindowStyle.Minimized
            };

            var process = Process.Start(psi);

            try
            {
                AssertEx.Throws<DebuggerInitializationException>(
                    () => CordbLauncher.Attach(new AttachProcessTargetOptions(process.Id) { FrameworkKind = FrameworkKind.NetCore }, null, default),
                    "however debugger process is"
                );
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill();
            }
        }
    }
}
