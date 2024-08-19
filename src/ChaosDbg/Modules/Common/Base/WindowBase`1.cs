using System;
using System.Windows;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    public class WindowBase<T> : Window where T : ViewModelBase
    {
        protected IServiceProvider ServiceProvider => App.ServiceProvider;

        protected TService GetRequiredService<TService>() => ServiceProvider.GetService<TService>();

        protected T ViewModel { get; }

        public WindowBase()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();
            DataContext = ViewModel;
        }
    }
}
