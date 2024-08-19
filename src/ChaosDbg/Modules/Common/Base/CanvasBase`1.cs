using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    class CanvasAutomationPeer : FrameworkElementAutomationPeer
    {
        public CanvasAutomationPeer(FrameworkElement owner) : base(owner)
        {
        }
    }

    public class CanvasBase<T> : Canvas where T : ViewModelBase
    {
        protected IServiceProvider ServiceProvider => App.ServiceProvider;

        protected TService GetRequiredService<TService>() => ServiceProvider.GetService<TService>();

        public T ViewModel { get; }

        public CanvasBase()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new CanvasAutomationPeer(this);
    }
}
