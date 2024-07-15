using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using ChaosDbg.Analysis;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.DbgEng.Server;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.IL;
using ChaosDbg.Logger;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [TestClass] //Required for our [AssemblyInitialize] handler to fire
    public abstract class BaseTest
    {
        public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; }

        [ThreadStatic]
        private IServiceProvider serviceProvider;

        private IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                {
                    var serviceCollection = new ServiceCollection
                    {
                        typeof(CordbEngineProvider),
                        typeof(DbgEngEngineProvider),
                        typeof(NativeLibraryProvider),
                        typeof(PEMetadataProvider),

                        typeof(CordbEngineServices),
                        typeof(DbgEngEngineServices),

                        typeof(ILDisassemblerProvider),

                        typeof(DbgEngRemoteClientProvider),

                        { typeof(IFrameworkTypeDetector), typeof(FrameworkTypeDetector) },
                        { typeof(IPEFileProvider), typeof(MockPEFileProvider) },
                        { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) },
                        { typeof(ISigReader), typeof(SigReader) }
                    };

                    serviceProvider = serviceCollection.Build();
                }

                return serviceProvider;
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            serviceProvider = null;
        }

        protected T GetService<T>() => ServiceProvider.GetService<T>();

        protected string EventName => $"ChaosDbg_Test_{Process.GetCurrentProcess().Id}_{TestContext.TestName}";

        protected void TestCreate(
            Either<TestType, NativeTestType> testType,
            bool is32Bit,
            Action<Process> validate,
            bool netCore = false,
            bool native = false)
        {
            var path = GetTestAppPath(is32Bit ? IntPtr.Size == 4 : IntPtr.Size == 8, netCore, native);

            Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());

            Process process = null;

            try
            {
                process = Process.Start(path, $"{(testType.IsLeft ? testType.Left.ToString() : ((int) testType.Right).ToString())} {EventName}");

                using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

                eventHandle.WaitOne();

                //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
                Thread.Sleep(100);

                validate(process);
            }
            finally
            {
                try
                {
                    if (process != null)
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                }
                catch
                {
                    //Ignore
                }
            }
        }

        //Launch a process via CordbEngine.CreateProcess, and have it signal an event to inform us it ran all the way to a point
        //where it can signal an event
        protected void TestSignalledDebugCreate(
            Either<TestType, NativeTestType> testType,
            Action<TestContext> action,
            bool matchCurrentProcess = true,
            bool netCore = false,
            bool useInterop = false,
            bool nativeTestApp = false,
            FrameworkKind? frameworkKind = null,
            bool waitForSignal = true,
            bool? handleLoaderBP = null,
            string customExe = null,
            Action<CordbEngineProvider> hookEvents = null)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                throw new InvalidOperationException("ICorDebug can only be interacted with from an MTA thread. Attempting to interact with ICorDebug (such as calling Stop()) will cause E_NOINTERFACE errors.");

            var cordbEngineProvider = GetService<CordbEngineProvider>();

            var cts = new CancellationTokenSource();

            if (handleLoaderBP == null && waitForSignal)
                handleLoaderBP = true;

            if (handleLoaderBP == true && useInterop)
            {
                var haveLoaderBP = false;

                cordbEngineProvider.EngineStatusChanged += (s, e) =>
                {
                    if (!haveLoaderBP && e.NewStatus == EngineStatus.Break)
                    {
                        haveLoaderBP = true;
                        cordbEngineProvider.ActiveEngine.Continue();
                    }
                };
            }

            string path;
            string commandLine;

            if (customExe == null)
            {
                path = GetTestAppPath(matchCurrentProcess, netCore, nativeTestApp);
                commandLine = $"\"{path}\" {(testType.IsLeft ? testType.Left.ToString() : ((int) testType.Right).ToString())} {EventName}";

                Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());
            }
            else
            {
                path = customExe;
                commandLine = path;
            }

            using var ctx = new TestContext(cordbEngineProvider, path);

            //Hook after creating the context so that default event handling occurs before our events
            hookEvents?.Invoke(cordbEngineProvider);

            cordbEngineProvider.EngineFailure += (s, e) =>
            {
                ctx.LastFatalException = e.Exception;
                cts.Cancel();

                ctx.LastFatalStatus = e.Status;
            };

            using var cordbEngine = (CordbEngine) cordbEngineProvider.CreateProcess(
                commandLine,
                startMinimized: true,
                useInterop: useInterop,
                frameworkKind: frameworkKind);

            SetThreadName($"[{cordbEngine.Session.EngineId}] {TestContext.TestName}");

            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

            var win32Process = cordbEngine.Process.Win32Process;

            using var optionsHolder = new DbgHelpOptionsHolder();

            try
            {
                if (waitForSignal)
                {
                    WaitHandle.WaitAny(new[] {eventHandle, cts.Token.WaitHandle});

                    if (ctx.LastFatalException != null)
                        throw ctx.LastFatalException;

                    //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
                    Thread.Sleep(100);

                    cordbEngine.Break();
                }

                ctx.InProcDbgEng = new Lazy<DbgEngEngine>(() =>
                {
                    //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                    var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();
                    var dbgEngEngine = dbgEngEngineProvider.Attach(cordbEngine.Process.Id, nonInvasive: true, noSuspend: true);
                    dbgEngEngine.WaitForBreak();

                    dbgEngEngine.Execute(".loadby sos clr");

                    return dbgEngEngine;
                });

                action(ctx);
            }
            finally
            {
                try
                {
                    if (!win32Process.HasExited)
                        win32Process.Kill();
                }
                catch
                {
                }
            }
        }

        protected void TestNativeMain(Action<TestContext> action)
        {
            TestSignalledDebugCreate(
                NativeTestType.Com,
                ctx =>
                {
                    ctx.OutOfProcDbgEng = new Lazy<DbgEngRemoteClient>(() =>
                    {
                        var clientProvider = GetService<DbgEngRemoteClientProvider>();

                        var remoteClient = clientProvider.CreateDebuggerServer(ctx.Process.Id);

                        return remoteClient;
                    });

                    //Wait for loader BP. We should hit an int 3
                    ctx.WaitForBreakpoint();
                    Assert.IsTrue(ctx.CurrentInstruction.Instruction.Code == Code.Int3);

                    ctx.MoveTo("wmain");

                    action(ctx);
                },
                useInterop: true,
                nativeTestApp: true,
                frameworkKind: FrameworkKind.NetFramework,
                waitForSignal: false
            );
        }

        protected void TestCLR(Action<TestContext> action)
        {
            TestSignalledDebugCreate(
                default,
                ctx =>
                {
                    ctx.WaitForBreakpoint();

                    _ = ctx.ActiveThread.StackTrace;

                    action(ctx);
                },
                useInterop: true,
                waitForSignal: false,
                customExe: "pwsh.exe"
            );
        }

        protected void TestDebugAttach(
            Either<TestType, NativeTestType> testType,
            Action<TestContext> action,
            bool netCore = false,
            bool useInterop = false,
            bool native = false)
        {
            TestCreate(
                testType,
                IntPtr.Size == 4,
                process =>
                {
                    //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
                    Thread.Sleep(100);

                    var cordbEngineProvider = GetService<CordbEngineProvider>();

                    using var cordbEngine = (CordbEngine) cordbEngineProvider.Attach(process.Id, useInterop);

                    //I don't really know the best way to wait for the initial attach events to complete yet, so for now we'll do this

                    Debug.WriteLine("Waiting for attach...");

                    while (cordbEngine.Session.IsAttaching)
                        Thread.Sleep(100);

                    Debug.WriteLine("!!! Got attach!");

                    cordbEngine.Break();

                    using var ctx = new TestContext(cordbEngineProvider, Process.GetProcessById(cordbEngine.Process.Id).ProcessName);

                    ctx.InProcDbgEng = new Lazy<DbgEngEngine>(() =>
                    {
                        //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                        var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();
                        var dbgEngEngine = dbgEngEngineProvider.Attach(cordbEngine.Process.Id, true);

                        Debug.WriteLine("Waiting for break...");

                        dbgEngEngine.WaitForBreak();

                        Debug.WriteLine("!!! Got break!");

                        return dbgEngEngine;
                    });

                    action(ctx);
                },
                netCore: netCore,
                native: native
            );
        }

        protected string GetTestAppPath(bool matchCurrentProcess, bool netCore, bool native = false)
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

            var baseDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dllPath), "..", "..", "..", "..", "artifacts", "bin", configuration, suffix));

            string filePath;

            if (native)
                filePath = Path.Combine(baseDir, $"Native.{suffix}.exe");
            else
                filePath = Path.Combine(baseDir, netCore ? "net5.0" : "net472", $"Managed.{suffix}.exe");

            return filePath;
        }
    }
}
