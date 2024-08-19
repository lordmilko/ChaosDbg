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
using ChaosLib;
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

        private static Type dbgHelpProviderType = typeof(OutOfProcDbgHelpProvider);

        private IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                    serviceProvider = CreateServiceProvider();

                return serviceProvider;
            }
        }

        protected IServiceProvider CreateServiceProvider(Action<ServiceCollection> configureServices = null)
        {
            var serviceCollection = new ServiceCollection
            {
                //chaos

                //Debug Engines
                typeof(DebugEngineProvider),

                //Debug Engine Service Collections
                typeof(CordbEngineServices),
                typeof(DbgEngEngineServices),

                //Symbols
                { typeof(IDbgHelpProvider), dbgHelpProviderType },

                //Native Library
                typeof(NativeLibraryProvider),
                { typeof(INativeLibraryLoadCallback[]), new[]
                {
                    typeof(DbgEngNativeLibraryLoadCallback),
                    typeof(DbgHelpNativeLibraryLoadCallback)
                }},

                //Console

                //Misc

                typeof(PEMetadataProvider),
                typeof(ILDisassemblerProvider),
                typeof(MicrosoftPdbSourceFileProvider),

                typeof(DbgEngRemoteClientProvider),

                { typeof(IUserInterface), typeof(NullUserInterface) },
                { typeof(IFrameworkTypeDetector), typeof(FrameworkTypeDetector) },
                { typeof(IPEFileProvider), typeof(MockPEFileProvider) },
                { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) },
                { typeof(ISigReader), typeof(SigReader) }
            };

            configureServices?.Invoke(serviceCollection);

            serviceProvider = serviceCollection.Build();

            return serviceProvider;
        }

        [AssemblyInitialize]
        public static unsafe void AssemblyInitialize(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext testContext)
        {
            //LdrRegisterDllNotification cannot be used in managed code; if an exception is thrown the CLR can deadlock trying to call
            //back into managed code

            GlobalProvider.AllowGlobalProvider = false;
            SerilogLogger.Install();

            NativeReflector.Initialize(
                new NativeLibraryProvider(),
                (IDbgHelpProvider) Activator.CreateInstance(dbgHelpProviderType)
            );
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            //LdrUnregisterDllNotification cannot be used in managed code; if an exception is thrown the CLR can deadlock trying to call
            //back into managed code

            //Close and flush any loggers
            Log.Shutdown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Log.ClearContext();
            Log.SetProperty("TestName", TestContext.TestName);
            Log.Information<BaseTest>("Starting test {TestName}");

            //We also try and set this if the threading model is wrong in MTATestMethodAttribute.Execute
            SetThreadName(TestContext.TestName);

            serviceProvider = null;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            (serviceProvider as IDisposable)?.Dispose();

            Log.Information<BaseTest>("Ending test");

            SetThreadName(null);
        }

        protected void SetThreadName(string value)
        {
            //.NET Framework doesn't let you set the thread name twice, but there's no reason why you can't do this
            ReflectionExtensions.SetFieldValue(Thread.CurrentThread, "m_Name", null);
            Thread.CurrentThread.Name = value;
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
                var psi = new ProcessStartInfo(path, $"{(testType.IsLeft ? testType.Left.ToString() : ((int) testType.Right).ToString())} {EventName}")
                {
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                process = Process.Start(psi);
                Log.Debug<BaseTest>("Created test process {name} (PID: {debuggeePid})", path, process.Id);

                using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

                if (!eventHandle.WaitOne(10000))
                    throw new TimeoutException();

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
            Action<DebugEngineProvider> hookEvents = null)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
                throw new InvalidOperationException("ICorDebug can only be interacted with from an MTA thread. Attempting to interact with ICorDebug (such as calling Stop()) will cause E_NOINTERFACE errors.");

            var engineProvider = GetService<DebugEngineProvider>();

            var cts = new CancellationTokenSource();

            if (handleLoaderBP == null && waitForSignal)
                handleLoaderBP = true;

            if (handleLoaderBP == true && useInterop)
            {
                var haveLoaderBP = false;

                engineProvider.EngineStatusChanged += (s, e) =>
                {
                    if (!haveLoaderBP && e.NewStatus == EngineStatus.Break)
                    {
                        haveLoaderBP = true;
                        ((CordbEngine) engineProvider.ActiveEngine).Continue();
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

            using var ctx = new TestContext(engineProvider, path);

            //Hook after creating the context so that default event handling occurs before our events
            hookEvents?.Invoke(engineProvider);

            engineProvider.EngineFailure += (s, e) =>
            {
                ctx.LastFatalException = e.Exception;
                cts.Cancel();

                ctx.LastFatalStatus = e.Status;
            };

            using var cordbEngine = engineProvider.Cordb.CreateProcess(
                commandLine,
                startMinimized: true,
                useInterop: useInterop,
                frameworkKind: frameworkKind);

            ctx.CordbEngine = cordbEngine;

            SetThreadName($"[{cordbEngine.Session.EngineId}] {TestContext.TestName}");

            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);

            var win32Process = cordbEngine.Process.Win32Process;

            using var optionsHolder = new DbgHelpOptionsHolder(((INativeStackWalkerFunctionTableProvider) cordbEngine.Process.Symbols).UnsafeGetDbgHelp());

            using var mainThread = Kernel32.OpenThread(ThreadAccess.THREAD_ALL_ACCESS, false, Kernel32.GetCurrentThreadId());
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;

            try
            {
                if (waitForSignal)
                {
                    if (WaitHandle.WaitAny(new[] {eventHandle, cts.Token.WaitHandle}, 10000) == WaitHandle.WaitTimeout)
                        Assert.Fail("Timed out waiting for an event");

                    if (ctx.LastFatalException != null)
                        throw ctx.LastFatalException;

                    //Sleep for a moment to allow the program to have actually entered Thread.Sleep() itself
                    Thread.Sleep(100);

                    cordbEngine.Break();
                }

                ctx.InProcDbgEng = new Lazy<DbgEngEngine>(() =>
                {
                    //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                    //We don't want to use the same event handlers that are being used for the CordbEngine instance
                    engineProvider.ClearEventHandlers();

                    engineProvider.EngineFailure += (s, e) =>
                    {
                        Debug.Assert(false, $"A fatal exception occurred within the {nameof(DbgEngEngine)}: {e.Exception}");
                    };

                    var output = new List<string>();

                    engineProvider.EngineOutput += (s, e) =>
                    {
                        output.Add(e.Text);
                    };

                    //Commented out because we're currently attempting to allow running multiple DbgEng tests in parallel to try and improve performance
                    //AssertDoNotParallelize();

                    var dbgEngEngine = engineProvider.DbgEng.Attach(
                        cordbEngine.Process.Id,
                        nonInvasive: true,
                        noSuspend: true,
                        useDbgEngSymOpts: true, //Allowed since we don't allow parallelization with the current test
                        dbgEngEngineId: cordbEngine.Session.EngineId);

                    if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
                    {
                        //We might now be on a random thread like the ICorDebug Win32 Callback Thread. If we create our DbgEng here, a DebugClient will be associated with this thread.
                        //But when we go to dispose our client, we'll want to do so on the main test thread. So ensure that the main thread has a DebugClient created on it

                        using var apcCompletedEvent = new ManualResetEventSlim(false);

                        PAPCFUNC apcCallback = _ =>
                        {
                            if (cts.IsCancellationRequested)
                                return;

                            Log.Debug<BaseTest>("Executed APC on thread {tid}", Kernel32.GetThreadId(mainThread));
                            dbgEngEngine.Session.AddUiClient();
                            apcCompletedEvent.Set();
                        };

                        Log.Debug<BaseTest>("Enqueuing APC for thread {tid}", Kernel32.GetThreadId(mainThread));
                        var apcResult = Kernel32.Native.QueueUserAPC(Marshal.GetFunctionPointerForDelegate(apcCallback), mainThread, IntPtr.Zero);

                        Log.Debug<BaseTest>("APC was enqueued with result {result}", apcResult);

                        Assert.AreEqual(1, apcResult);

                        WaitHandle.WaitAny(new[]{apcCompletedEvent.WaitHandle, cts.Token.WaitHandle});

                        if (cts.IsCancellationRequested)
                        {
                            dbgEngEngine.Dispose();
                            cts.Token.ThrowIfCancellationRequested();
                        }
                    }

                    //If the lazy throws before it's successfully created, I think it won't say it was created, so we won't know
                    //we need to dispose anthing
                    using var holder = new DisposeHolder(dbgEngEngine);
                    
                    dbgEngEngine.WaitForBreak();

                    dbgEngEngine.Execute(".loadby sos clr");

                    holder.SuppressDispose();

                    return dbgEngEngine;
                });

                ctx.OutOfProcDbgEng = new Lazy<DbgEngRemoteClient>(() =>
                {
                    var clientProvider = GetService<DbgEngRemoteClientProvider>();

                    var remoteClient = clientProvider.CreateOutOfProcDebuggerServer(cordbEngine.Process.Id);

                    return remoteClient;
                });

                try
                {
                    action(ctx);
                }
                finally
                {
                    /* If we've queued from the Win32 Event Thread to the main thread for the purposes of creating a DebugClient on it, but we, on the main
                     * thread right now, throw an exception, we're going to try and Dispose the CordbEngine which may result in a call to CorDebugProcess.Stop()
                     * if the process is not synchronized. However, this causes a big problem, because this result in a call to CordbWin32EventThread::SendUnmanagedContinue
                     * which then calls WaitForSingleObject() as it waits for the Win32 Event Thread to execute the stop request. You would think that, by
                     * calling WaitForSingleObject that that means that the thread will now enter an alertable state, however this is not the case: you have to call
                     * WaitForSingleObjectEx() with bAlertable: TRUE in order to enter an alertable state. Thus, we're now deadlocked. So, to mitigate this
                     * we have the Win32 Event Thread also wait on our CTS WaitHandle. If we cancel it, it'll give up waiting for the APC, and return so that
                     * the Win32 Event Thread can process the stop event */
                    cts.Cancel();

                    //Wait for any running background threads to end, and if any of them crashed, we'll have captured a fatal exception by the time this returns
                    ctx.Dispose();
                }

                if (ctx.LastFatalException != null)
                    ExceptionDispatchInfo.Capture(ctx.LastFatalException).Throw();
            }
            finally
            {
                engineProvider.ClearEventHandlers(); //For loop stress testing

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

                        var remoteClient = clientProvider.CreateOutOfProcDebuggerServer(ctx.Process.Id);

                        return remoteClient;
                    });

                    //Wait for loader BP. We should hit an int 3
                    Log.Debug<BaseTest>("Waiting for loader BP");
                    ctx.WaitForBreakpoint();
                    Assert.IsTrue(ctx.CurrentInstruction.Instruction.Code == Code.Int3);

                    Log.Debug<BaseTest>("Moving to wmain");
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

                    var engineProvider = GetService<DebugEngineProvider>();

                    var cts = new CancellationTokenSource();
                    Exception fatalException = null;

                    engineProvider.EngineFailure += (s, e) =>
                    {
                        fatalException = e.Exception;
                        cts.Cancel();
                    };

                    if (useInterop)
                    {
                        var haveLoaderBP = false;

                        engineProvider.EngineStatusChanged += (s, e) =>
                        {
                            if (!haveLoaderBP && e.NewStatus == EngineStatus.Break)
                            {
                                haveLoaderBP = true;
                                ((CordbEngine) engineProvider.ActiveEngine).Continue();
                            }
                        };
                    }

                    using var cordbEngine = engineProvider.Cordb.Attach(process.Id, useInterop);

                    SetThreadName($"[{cordbEngine.Session.EngineId}] {TestContext.TestName}");

                    //I don't really know the best way to wait for the initial attach events to complete yet, so for now we'll do this

                    Debug.WriteLine("Waiting for attach...");

                    for (var i = 0; i < 200; i++)
                    {
                        if (fatalException != null)
                            ExceptionDispatchInfo.Capture(fatalException).Throw();

                        if (cordbEngine.Session.IsAttaching)
                            cts.Token.WaitHandle.WaitOne(100);
                        else
                            break;
                    }

                    if (fatalException != null)
                        ExceptionDispatchInfo.Capture(fatalException).Throw();

                    if (cordbEngine.Session.IsAttaching)
                        throw new TimeoutException("Timed out waiting for attach");

                    Debug.WriteLine("!!! Got attach!");

                    cordbEngine.Break();

                    using var ctx = new TestContext(engineProvider, Process.GetProcessById(cordbEngine.Process.Id).ProcessName);

                    ctx.CordbEngine = cordbEngine;

                    ctx.InProcDbgEng = new Lazy<DbgEngEngine>(() =>
                    {
                        //Note: creating a DebugClient will cause DbgHelp's global options to be modified

                        //We don't want to use the same event handlers that are being used for the CordbEngine instance
                        engineProvider.ClearEventHandlers();

                        engineProvider.EngineFailure += (s, e) =>
                        {
                            Debug.Assert(false, $"A fatal exception occurred within the {nameof(DbgEngEngine)}: {e.Exception}");
                        };

                        //Commented out because we're currently attempting to allow running multiple DbgEng tests in parallel to try and improve performance
                        //AssertDoNotParallelize();

                        var dbgEngEngine = engineProvider.DbgEng.Attach(cordbEngine.Process.Id, nonInvasive: true, useDbgEngSymOpts: false, dbgEngEngineId: cordbEngine.Session.EngineId);

                        Debug.WriteLine("Waiting for break...");

                        dbgEngEngine.WaitForBreak();

                        Debug.WriteLine("!!! Got break!");

                        return dbgEngEngine;
                    });

                    action(ctx);

                    //If we're executing this in a loop as part of a stress test, if we don't clear the event handlers, they'll just keep accumulating with each run
                    engineProvider.ClearEventHandlers();
                },
                netCore: netCore,
                native: native
            );
        }

        private void AssertDoNotParallelize()
        {
            var testMethod = GetType().GetMethod(TestContext.ManagedMethod);

            var attrib = testMethod.GetCustomAttribute<DoNotParallelizeAttribute>();

            //This comment isn't really accurate anymore; we use an out-of-proc DbgHelpSrv, I think the issue is more-so that we don't want to overwrite
            //g_Machine, which we protect using a lock on the DebugEngineProvider
            if (attrib == null)
                Debug.Assert(false, $"{TestContext.ManagedMethod}: Methods that use InProc DbgEng should use DoNotParallelizeAttribute so that DbgEng SymOpts can be modified");
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
