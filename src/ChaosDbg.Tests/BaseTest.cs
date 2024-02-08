using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChaosDbg.Analysis;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    public class CallbackContext : IDisposable
    {
        public CordbEngine CordbEngine { get; }

        public Lazy<DbgEngEngine> DbgEngEngine { get; }

        public CallbackContext(CordbEngine cordbEngine, Lazy<DbgEngEngine> dbgEngEngine)
        {
            CordbEngine = cordbEngine;
            DbgEngEngine = dbgEngEngine;
        }

        public void Dispose()
        {
            CordbEngine?.Dispose();

            if (DbgEngEngine.IsValueCreated)
                DbgEngEngine.Value.Dispose();
        }
    }

    public abstract class BaseTest
    {
        public TestContext TestContext { get; set; }

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

                        { typeof(IExeTypeDetector), typeof(ExeTypeDetector) },
                        { typeof(IPEFileProvider), typeof(PEFileProvider) },
                        { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) },
                        { typeof(ISigReader), typeof(SigReader) }
                    };

                    serviceProvider = serviceCollection.Build();
                }

                return serviceProvider;
            }
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

        protected void TestDebugCreate(
            Either<TestType, NativeTestType> testType,
            Action<CallbackContext> action,
            bool matchCurrentProcess = true,
            bool netCore = false,
            bool useInterop = false,
            bool native = false,
            ExeKind? exeKind = null)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                throw new InvalidOperationException("ICorDebug can only be interacted with from an MTA thread. Attempting to interact with ICorDebug (such as calling Stop()) will cause E_NOINTERFACE errors.");

            var cordbEngineProvider = GetService<CordbEngineProvider>();

            var path = GetTestAppPath(matchCurrentProcess, netCore, native);

            Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());

            using var cordbEngine = (CordbEngine) cordbEngineProvider.CreateProcess(
                $"\"{path}\" {(testType.IsLeft ? testType.Left.ToString() : ((int) testType.Right).ToString())} {EventName}",
                useInterop: useInterop,
                exeKind: exeKind);

            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

            var win32Process = cordbEngine.Process.Win32Process;

            try
            {
                eventHandle.WaitOne();

                //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
                Thread.Sleep(100);

                cordbEngine.Break();

                using var ctx = new CallbackContext(cordbEngine, new Lazy<DbgEngEngine>(() =>
                {
                    //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                    var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();
                    var dbgEngEngine = dbgEngEngineProvider.Attach(cordbEngine.Process.Id, nonInvasive: true, noSuspend: true);
                    dbgEngEngine.WaitForBreak();

                    dbgEngEngine.Execute(".loadby sos clr");

                    return dbgEngEngine;
                }));

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

        protected void TestDebugAttach(
            Either<TestType, NativeTestType> testType,
            Action<CallbackContext> action,
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

                    using var ctx = new CallbackContext(cordbEngine, new Lazy<DbgEngEngine>(() =>
                    {
                        //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                        var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();
                        var dbgEngEngine = dbgEngEngineProvider.Attach(cordbEngine.Process.Id, true);

                        Debug.WriteLine("Waiting for break...");

                        dbgEngEngine.WaitForBreak();

                        Debug.WriteLine("!!! Got break!");

                        return dbgEngEngine;
                    }));

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
