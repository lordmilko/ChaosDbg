using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using ChaosDbg.Engine;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUIWindow = FlaUI.Core.AutomationElements.Window;

namespace ChaosDbg.Tests
{
    /// <summary>
    /// Provides facilities for running a WPF application.
    /// </summary>
    class AppRunner
    {
        static AppRunner()
        {
            Application.ResourceAssembly = typeof(App).Assembly;
        }

        private static object lockObj = new object();

        public static Thread Run(CancellationToken token)
        {
            var ap = Application.Current;
            var thread = new Thread(App.Main);
            thread.Name = "AppRunnerThread";
            thread.SetApartmentState(ApartmentState.STA);

            thread.Start();

            token.Register(() => Invoke(a => a.Shutdown()));

            return thread;
        }

        public static void Invoke(Action<Application> action) =>
            Application.Current.Dispatcher.Invoke(() => action(Application.Current));

        private static T Invoke<T>(Func<Application, T> func) =>
            Application.Current.Dispatcher.Invoke(() => func(Application.Current));

        /// <summary>
        /// Executes an application within the current process, allowing modifications to its internal services prior to startup.
        /// </summary>
        /// <param name="action">The action to perform during the lifetime of the application.</param>
        /// <param name="configureServices">An optional action that allows modifying or mocking the services of the application prior to startup.</param>
        public static void WithInProcessApp(Action<FlaUIWindow> action, Action<ServiceCollection> configureServices = null)
        {
            lock (lockObj)
            {
                GlobalProvider.ConfigureServices = configureServices;

                var cts = new CancellationTokenSource();
                var thread = Run(cts.Token);

                var attempts = 0;

                while (Application.Current == null)
                {
                    attempts++;

                    if (attempts > 100)
                        throw new TimeoutException("Application did not load in a timely manner");

                    cts.Token.WaitHandle.WaitOne(10);
                }

                if (!thread.IsAlive)
                    throw new InvalidOperationException("Application thread ended prematurely");

                bool mainWindowLoaded = false;
                IntPtr hwnd = IntPtr.Zero;

                attempts = 0;

                while (!mainWindowLoaded)
                {
                    attempts++;

                    if (attempts > 100)
                        throw new TimeoutException("MainWindow did not load in a timely manner");

                    Invoke(a =>
                    {
                        if (a.MainWindow == null)
                            return;

                        hwnd = new WindowInteropHelper(a.MainWindow).Handle;

                        if (hwnd != IntPtr.Zero)
                            mainWindowLoaded = true;
                    });

                    cts.Token.WaitHandle.WaitOne(10);
                }

                try
                {
                    using (var automation = new UIA3Automation())
                    {
                        var window = automation.FromHandle(hwnd).AsWindow();

                        action(window);
                    }
                }
                finally
                {
                    GlobalProvider.ConfigureServices = null;
                    cts.Cancel();
                }                
            }
        }

        /// <summary>
        /// Executes an application out of process, optionally attaching to it with the Visual Studio debugger that is debugging this process.
        /// </summary>
        /// <param name="action">The action to perform during the lifetime of the application.</param>
        /// <param name="debug">Whether to attach the Visual Studio debugger to the process.</param>
        public static void WithOutOfProcessApp(Action<FlaUIWindow> action, bool debug = false)
        {
            lock (lockObj)
            {
                var path = typeof(App).Assembly.Location;

                var app = FlaUI.Core.Application.Launch(path);

                try
                {
                    if (debug)
                        VsDebugger.Attach(Process.GetProcessById(app.ProcessId));

                    using (var automation = new UIA3Automation())
                    {
                        var window = app.GetMainWindow(automation);

                        action(window);
                    }
                }
                finally
                {
                    app.Close();
                }
            }
        }
    }
}
