using System;
using System.Windows;
using ChaosDbg.Engine;
using ChaosLib;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IUserInterface
    {
        public static IServiceProvider ServiceProvider => GlobalAppServices.Current.ServiceProvider;

        public new static App Current => GlobalAppServices.Current.App;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class for use with the <see cref="GlobalProvider.ServiceProvider"/>.
        /// </summary>
        public App()
        {
            var serviceProvider = GlobalProvider.ServiceProvider;
            ((ServiceProvider) serviceProvider).AddSingleton(typeof(IUserInterface), this);
            GlobalAppServices.Add(this, serviceProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class for use with a specific <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that should be associated with this <see cref="App"/>.</param>
        public App(IServiceProvider serviceProvider)
        {
            ((ServiceProvider) serviceProvider).AddSingleton(typeof(IUserInterface), this);
            GlobalAppServices.Add(this, serviceProvider);
        }

        void IUserInterface.HandleEvent<T>(EventHandler<T> handler, object sender, T args)
        {
            //Asynchronously dispatch to the UI thread so that if we're on the engine thread, we don't deadlock
            //if the event handler on UI thread tries to invoke a command back on the engine thread
            if (handler != null)
                Dispatcher.InvokeAsync(() => handler.Invoke(sender, args));
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            var serviceProvider = ServiceProvider;

            (serviceProvider as IDisposable)?.Dispose();

            GlobalAppServices.Remove(this);

            //AllowGlobalProvider will be false when unit testing, and true otherwise
            if (GlobalProvider.AllowGlobalProvider)
                Log.Shutdown();
        }
    }
}
