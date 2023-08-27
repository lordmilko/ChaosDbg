using System;
using System.Windows.Controls;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    public class CanvasBase<T> : Canvas where T : ViewModelBase
    {
        protected IServiceProvider ServiceProvider => GlobalProvider.ServiceProvider;

        protected TService GetRequiredService<TService>() => ServiceProvider.GetService<TService>();

        protected T ViewModel { get; }

        public CanvasBase()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();
        }
    }
}
