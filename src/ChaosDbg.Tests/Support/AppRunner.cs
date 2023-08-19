using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using ChaosDbg.Engine;

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
            thread.SetApartmentState(ApartmentState.STA);

            thread.Start();

            token.Register(() => Invoke(a => a.Shutdown()));

            return thread;
        }

        private static void Invoke(Action<Application> action) =>
            Application.Current.Dispatcher.Invoke(() => action(Application.Current));

        private static T Invoke<T>(Func<Application, T> func) =>
            Application.Current.Dispatcher.Invoke(() => func(Application.Current));

        public static void WithApp(Action<IntPtr> action, Action<ServiceCollection> configureServices = null)
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
                    action(hwnd);
                }
                finally
                {
                    GlobalProvider.ConfigureServices = null;
                    cts.Cancel();
                }                
            }
        }
    }
}