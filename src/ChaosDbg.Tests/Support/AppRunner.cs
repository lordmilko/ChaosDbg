using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using ChaosDbg.Engine;
using ChaosLib;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUIWindow = FlaUI.Core.AutomationElements.Window;
using Window = System.Windows.Window;

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

        public static Thread Run(CancellationToken token) =>
            Run(null, token);

        public static Thread Run(Func<Window> createWindow, CancellationToken token)
        {
            var thread = new Thread(() =>
            {
                Log.Debug<AppRunner>("Creating app");

                typeof(Application).GetField("_appCreatedInThisAppDomain", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                var app = new App();
                app.InitializeComponent();

                using (new PreloadedPackageProtector())
                {
                    if (createWindow == null)
                    {
                        Log.Debug<AppRunner>("Running app with default window");

                        app.Run();

                        Log.Debug<AppRunner>("App completed successfully");
                    }
                    else
                    {
                        //Don't start the default window. The StartupUri property won't let us clear its value,
                        //so we must use reflection
                        typeof(Application).GetFieldInfo("_startupUri").SetValue(app, null);

                        Log.Debug<AppRunner>("Running app with custom window");

                        //Our alternate window must be created on the same thread as the App
                        app.Run(createWindow());

                        Log.Debug<AppRunner>("App completed successfully");
                    }
                }

                //Create a new GlobalProvider for the next test
                typeof(GlobalProvider).TypeInitializer.Invoke(null, null);
            });
            Log.CopyContextTo(thread);
            thread.Name = "AppRunnerThread";
            thread.SetApartmentState(ApartmentState.STA);

            thread.Start();

            token.Register(() =>
            {
                if ((bool) typeof(Application).GetProperty("IsShuttingDown", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))
                    return;

                Invoke(a =>
                {
                    try
                    {
                        Debug.WriteLine("calling shutdown");
                        a?.Shutdown();
                    }
                    catch
                    {
                    }
                });
            });

            return thread;
        }

        public static void Invoke(Action<Application> action)
        {
            Exception outerEx = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Log.Debug<AppRunner>("Executing action on dispatcher thread");
                    action(Application.Current);
                }
                catch (Exception ex)
                {
                    outerEx = ex;
                }
            }, DispatcherPriority.Send, default, TimeSpan.FromSeconds(10));

            if (outerEx != null)
                throw outerEx;
        }

        private static T Invoke<T>(Func<Application, T> func) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Debug<AppRunner>("Executing func on dispatcher thread");
                return func(Application.Current);
            }, DispatcherPriority.Send, default, TimeSpan.FromSeconds(10));

        #region WPF

        public static void WithInProcessApp(Action<MainWindow> action, Action<ServiceCollection> configureServices = null) =>
            WithInProcessApp(null, action, configureServices);

        public static void WithInProcessApp<T>(
            Func<T> createWindow,
            Action<T> action,
            Action<ServiceCollection> configureServices = null,
            Func<T, bool> waitFor = null) where T : Window
        {
            WithInProcessAppInternal(createWindow, _ =>
            {
                Invoke(a => action((T) a.MainWindow));
            }, configureServices, waitFor == null ? (Func<Window, bool>) null : w => waitFor((T) w));
        }

        #endregion
        #region FlaUI

        /// <summary>
        /// Executes an application within the current process, allowing modifications to its internal services prior to startup.
        /// </summary>
        /// <param name="action">The action to perform during the lifetime of the application.</param>
        /// <param name="configureServices">An optional action that allows modifying or mocking the services of the application prior to startup.</param>
        public static void WithInProcessApp(Action<FlaUIWindow> action, Action<ServiceCollection> configureServices = null) =>
            WithInProcessApp(null, action, configureServices);

        public static void WithInProcessApp(
            Func<Window> createWindow,
            Action<FlaUIWindow> action,
            Action<ServiceCollection> configureServices = null)
        {
            WithInProcessAppInternal(createWindow, hwnd =>
            {
                using (var automation = new UIA3Automation())
                {
                    var flaWindow = automation.FromHandle(hwnd).AsWindow();

                    action(flaWindow);
                }
            }, configureServices);
        }

        #endregion

        private static void WithInProcessAppInternal(
            Func<Window> createWindow,
            Action<IntPtr> action,
            Action<ServiceCollection> configureServices = null,
            Func<Window, bool> waitFor = null)
        {
            lock (lockObj)
            {
                GlobalProvider.ConfigureServices = configureServices;

                var cts = new CancellationTokenSource();
                var thread = Run(createWindow, cts.Token);

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

                if (waitFor != null)
                {
                    var conditionMet = false;

                    while (!conditionMet)
                    {
                        Invoke(a =>
                        {
                            if (waitFor(a.MainWindow))
                                conditionMet = true;
                        });

                        if (!conditionMet)
                            cts.Token.WaitHandle.WaitOne(10);
                    }
                }

                try
                {
                    action(hwnd);
                }
                finally
                {
                    GlobalProvider.ConfigureServices = null;

                    try
                    {
                        Invoke(a => a?.Shutdown());
                        cts.Cancel();
                    }
                    catch
                    {
                    }
                }

                thread.Join();
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
                        VsDebugger.Attach(Process.GetProcessById(app.ProcessId), VsDebuggerType.Managed);

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

        /// <summary>
        /// Executes an application in process with a dummy window containing the specified content.
        /// </summary>
        /// <param name="element">The content to include in the window.</param>
        /// <param name="action">The action to perform during the lifetime of the application.</param>
        public static void WithCustomXaml(XamlElement element, Action<Window> action)
        {
            Func<Window> createWindow = () =>
            {
                var window = new TestWindow();

                var context = new ParserContext()
                {
                    XmlnsDictionary =
                    {
                        { string.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation" },
                        { "x", "http://schemas.microsoft.com/winfx/2006/xaml" },
                        { "local", "clr-namespace:ChaosDbg;assembly=ChaosDbg" }
                    }
                };

                var result = XamlReader.Parse(element.ToString(), context);

                window.Content = result;

                return window;
            };

            WithInProcessApp(createWindow, action);
        }
    }
}
