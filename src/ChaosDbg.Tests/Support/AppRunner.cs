using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using ChaosDbg.Engine;
using ChaosLib;
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

        private static object appLock = new object();

        public static (App app, Thread thread) Run(CancellationToken token) =>
            Run(null, null, token);

        public static (App app, Thread thread) Run(Func<Window> createWindow, Action<ServiceCollection> configureServices, CancellationToken token)
        {
            App app = null;
            var appCreated = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                Log.Debug<AppRunner>("Creating app");

                app = CreateApp(configureServices);
                appCreated.Set();
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

            token.Register(()=> StopApp(app));

            WaitHandle.WaitAny(new[] {appCreated.WaitHandle, token.WaitHandle});
            token.ThrowIfCancellationRequested();

            Debug.Assert(app != null);

            return (app, thread);
        }

        public static void Invoke(App app, Action<App> action)
        {
            Exception outerEx = null;

            app.Dispatcher.Invoke(() =>
            {
                try
                {
                    Log.Debug<AppRunner>("Executing action on dispatcher thread");
                    action(app);
                }
                catch (Exception ex)
                {
                    outerEx = ex;
                }
            }, DispatcherPriority.Send, default, TimeSpan.FromSeconds(10));

            if (outerEx != null)
                ExceptionDispatchInfo.Capture(outerEx).Throw();
        }

        private static T Invoke<T>(App app, Func<App, T> func) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Debug<AppRunner>("Executing func on dispatcher thread");
                return func(app);
            }, DispatcherPriority.Send, default, TimeSpan.FromSeconds(10));

        #region WPF

        public static void WithInProcessApp(Action<App, MainWindow> action, Action<ServiceCollection> configureServices = null) =>
            WithInProcessApp(null, action, configureServices);

        public static void WithInProcessApp<T>(
            Func<T> createWindow,
            Action<App, T> action,
            Action<ServiceCollection> configureServices = null,
            Func<T, bool> waitFor = null) where T : Window
        {
            WithInProcessAppInternal(createWindow, (_, app) =>
            {
                Invoke(app, a => action(app, (T) a.MainWindow));
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
            WithInProcessAppInternal(createWindow, (hwnd, _) =>
            {
                using (var automation = new UIA3Automation())
                {
                    var flaWindow = automation.CreateWindowSafe(hwnd);

                    action(flaWindow);
                }
            }, configureServices);
        }

        #endregion

        private static void WithInProcessAppInternal(
            Func<Window> createWindow,
            Action<IntPtr, App> action,
            Action<ServiceCollection> configureServices = null,
            Func<Window, bool> waitFor = null)
        {
            var cts = new CancellationTokenSource();
            var (app, thread) = Run(createWindow, configureServices, cts.Token);

            if (!thread.IsAlive)
                throw new InvalidOperationException("Application thread ended prematurely");

            bool mainWindowLoaded = false;
            IntPtr hwnd = IntPtr.Zero;

            var attempts = 0;

            while (!mainWindowLoaded)
            {
                attempts++;

                if (attempts > 100)
                    throw new TimeoutException("MainWindow did not load in a timely manner");

                Invoke(app, a =>
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
                    Invoke(app, a =>
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
                action(hwnd, app);
            }
            finally
            {
                try
                {
                    //It's the responsibility of the registration that was made on the CancellationToken to stop the app.
                    //We don't want to complicate things by having two avenues for calling stop
                    cts.Cancel();
                }
                catch
                {
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
            var path = typeof(App).Assembly.Location;

            var app = FlaUI.Core.Application.Launch(path);

            try
            {
                if (debug)
                    VsDebugger.Attach(Process.GetProcessById(app.ProcessId), VsDebuggerType.Managed);

                using (var automation = new UIA3Automation())
                {
                    var window = app.GetMainWindowSafe(automation);

                    action(window);
                }
            }
            finally
            {
                app.Close();
            }
        }

        /// <summary>
        /// Executes an application in process with a dummy window containing the specified content.
        /// </summary>
        /// <param name="element">The content to include in the window.</param>
        /// <param name="action">The action to perform during the lifetime of the application.</param>
        public static void WithCustomXaml(XamlElement element, Action<App, Window> action)
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
                        { "local", "clr-namespace:ChaosDbg;assembly=ChaosDbg" },
                        { "view", "clr-namespace:VsDock.View;assembly=VsDock" }
                    }
                };

                var result = XamlReader.Parse(element.ToString(), context);

                window.Content = result;

                return window;
            };

            WithInProcessApp(createWindow, action);
        }

        private static App CreateApp(Action<ServiceCollection> configureServices)
        {
            lock (appLock)
            {
                var serviceProvider = GlobalProvider.CreateServiceProvider(configureServices);
                var app = new App(serviceProvider);
                typeof(Application).GetField("_appCreatedInThisAppDomain", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                typeof(Application).GetField("_appInstance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
                return app;
            }
        }

        private static void StopApp(App app)
        {
            //IsShuttingDown is static

            lock (appLock)
            {
                var isShuttingDownPropertyInfo = typeof(Application).GetProperty("IsShuttingDown", BindingFlags.Static | BindingFlags.NonPublic);

                if ((bool) isShuttingDownPropertyInfo.GetValue(null))
                    return;

                Invoke(app, a =>
                {
                    try
                    {
                        /* During WPF shutdown, the method Application.DoShutdown() iterates over all windows in Application.WindowsInternal.
                         * If a given window has not been disposed yet, it calls InternalClose() on it, which ultimately results in a WM_DESTROY
                         * message being dispatched, leading to the window removing itself from the WindowCollection in Window.UpdateWindowListsOnClose().
                         * However, this only occurs when the window sees that Application.Current is not null. In the event that the window is already destroyed,
                         * but is still in the WindowsInternal list, DoShutdown() attempts to call WindowCollection.RemoveAt(0) on it. However, WindowCollection.RemoveAt
                         * contains a bug, wherein it then attempts to pass this index as the "object" to remove in ArrayList.Remove(). Because we clear out Application.Current
                         * in order to run multiple applications in parallel, we'll never correctly remove the window from WindowsInternal and always get stuck in the bug path.
                         * Thus, we pre-emptively close all our windows prior to requesting that the application shutdown */
                        
                        if (a != null)
                        {
                            var remove = typeof(WindowCollection).GetMethodInfo("Remove");

                            //The Application.Windows property creates a clone. We want to modify the internal collection
                            var windows = (WindowCollection) typeof(Application).GetPropertyInfo("WindowsInternal").GetValue(a);

                            while (windows.Count > 0)
                            {
                                var window = windows[0];
                                window.Close();

                                //We call Remove instead of RemoveAt which WPF will do during shutdown which erroneously then calls ArrayList.Remove with the object being the index
                                remove.Invoke(windows, new[] {window});
                            }

                            Debug.Assert(a.Windows.Count == 0);
                        }

                        a?.Shutdown();
                    }
                    catch
                    {
                    }
                });

                isShuttingDownPropertyInfo.SetValue(null, false);
            }
        }
    }
}
