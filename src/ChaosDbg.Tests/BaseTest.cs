using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
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
                        typeof(DbgEngEngine),
                        typeof(NativeLibraryProvider),

                        typeof(CordbEngineServices),

                        { typeof(IExeTypeDetector), typeof(ExeTypeDetector) },
                        { typeof(ICordbEngine), typeof(CordbEngine) },
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
            TestType testType,
            bool is32Bit,
            Action<Process> validate,
            bool netCore = false)
        {
            var path = GetTestAppPath(is32Bit ? IntPtr.Size == 4 : IntPtr.Size == 8, netCore);

            Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());

            Process process = null;

            try
            {
                process = Process.Start(path, $"{testType} {EventName}");

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
                    process?.Kill();
                }
                catch
                {
                    //Ignore
                }
            }
        }

        protected void TestDebugCreate(
            TestType testType,
            Action<CordbEngine> action,
            bool matchCurrentProcess = true,
            bool netCore = false,
            bool useInterop = false)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                throw new InvalidOperationException("ICorDebug can only be interacted with from an MTA thread. Attempting to interact with ICorDebug (such as calling Stop()) will cause E_NOINTERFACE errors.");

            using var engine = (CordbEngine) GetService<ICordbEngine>();

            var path = GetTestAppPath(matchCurrentProcess, netCore);

            Environment.SetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID", Process.GetCurrentProcess().Id.ToString());

            engine.CreateProcess($"\"{path}\" {testType} {EventName}", useInterop: useInterop);

            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

            eventHandle.WaitOne();

            //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
            Thread.Sleep(100);

            engine.Break();

            action(engine);
        }

        protected string GetTestAppPath(bool matchCurrentProcess, bool netCore)
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
