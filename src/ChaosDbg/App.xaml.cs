using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            EventExtensions.DispatchAsync = action =>
            {
                var dispatcher = Current?.Dispatcher;

                if (dispatcher == null)
                    return false;

                dispatcher.InvokeAsync(action);
                return true;
            };

            base.OnStartup(e);
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            GlobalProvider.Dispose();
        }
    }
}
