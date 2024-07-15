using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TestApp
{
    class Program
    {
        internal static string eventName;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SignalReady()
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

            Console.WriteLine("Signalled!");

            eventHandle.Set();

            while (true)
                Thread.Sleep(1);
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Test type and event name must be specified");
                Environment.Exit(1);
            }

            BindLifetimeToParentProcess();

            eventName = args[1];

            var testType = (TestType) Enum.Parse(typeof(TestType), args[0]);

            Console.WriteLine($"Dispatching test {testType}");

            switch (testType)
            {
                case TestType.CordbEngine_Thread_StackTrace_ManagedFrames:
                    new CordbEngine_Thread_StackTrace().Managed();
                    break;

                case TestType.CordbEngine_Thread_StackTrace_InternalFrames:
                    new CordbEngine_Thread_StackTrace().Internal();
                    break;

                case TestType.CordbEngine_Thread_TLS:
                {
                    for (var i = 0; i < 20; i++)
                    {
                        var slot = NativeMethods.TlsAlloc();

                        NativeMethods.TlsSetValue(slot, new IntPtr(i));
                    }

                    SignalReady();

                    break;
                }

                case TestType.CordbEngine_Thread_TLS_Extended:
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var slot = NativeMethods.TlsAlloc();

                        NativeMethods.TlsSetValue(slot, new IntPtr(i));
                    }

                    SignalReady();

                    break;
                }

                case TestType.CordbEngine_Thread_Type:
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        SignalReady();
                    });

                    while (true)
                        Thread.Sleep(1);

                case TestType.DbgEngEngine_ChildProcess:
                    var childProcess = Process.Start(Assembly.GetExecutingAssembly().Location, $"{TestType.CordbEngine_Thread_StackTrace_ManagedFrames} {args[1]}_Child");
                    Console.WriteLine($"Started child process {childProcess.Id}");

                    SignalReady();
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(TestType)} '{testType}'.");
            }
        }

        private static void BindLifetimeToParentProcess()
        {
            var str = Environment.GetEnvironmentVariable("CHAOSDBG_TEST_PARENT_PID");

            if (!string.IsNullOrEmpty(str) && int.TryParse(str, out var val))
            {
                var parent = Process.GetProcessById(val);

                parent.EnableRaisingEvents = true;
                parent.Exited += (s, o) => Process.GetCurrentProcess().Kill();
            }
        }
    }
}
