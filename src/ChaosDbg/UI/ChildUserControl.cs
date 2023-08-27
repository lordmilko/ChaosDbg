using System;
using System.Windows;
using System.Windows.Controls;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a custom <see cref="UserControl"/> containing a view model but without an explicitly specified <see cref="FrameworkElement.BindingGroup"/>.<para/>
    /// Controls derived from this type are capable of receiving custom properties bound using the <c>Binding</c> markup extension, i.e.
    /// i.e. <c>MyProperty="{Binding SomeProperty}</c>".
    /// </summary>
    /// <typeparam name="T">The type of view model this control encapsulates.</typeparam>
    public class ChildUserControl<T> : UserControl where T : ViewModelBase
    {
        /// <inheritdoc cref="GlobalProvider.ServiceProvider" />
        protected IServiceProvider ServiceProvider => GlobalProvider.ServiceProvider;

        protected T ViewModel { get; }

        //The WPF designer will get upset if this constructor is protected
        public ChildUserControl()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();
        }
    }
}
