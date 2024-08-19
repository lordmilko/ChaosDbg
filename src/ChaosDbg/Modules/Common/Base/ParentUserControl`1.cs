using System;
using System.Windows;
using System.Windows.Controls;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /* Adding a new control:
     
       MyNewControl.xaml
       1. Change the x:Class to be ChaosDbg.MyNewControl
       2. Change the tag type from UserControl to local:UserControlEx
       3. Change xmlns:local to be ChaosDbg
       4. Before mc:Ignoreable="d" add the following attributes

          xmlns:vm="clr-namespace:ChaosDbg.ViewModel;assembly=ChaosDbg.Engine"
          x:TypeArguments="vm:MyNewControlViewModel"
          d:DataContext="{d:DesignInstance Type=vm:MyNewControlViewModel}"

       MyNewControl.xaml.cs
       1. Change the namespace to ChaosDbg
       2. Change the base class to UserControlEx<MyNewControlViewModel>
    */

    /// <summary>
    /// Represents a custom <see cref="UserControl"/> containing a <see cref="FrameworkElement.DataContext"/> capable
    /// of passing properties from its view model to child components.<para/>
    /// Custom properties defined on controls that derive from this type cannot be bound to using the <c>Binding</c> markup extension,
    /// i.e. <c>MyProperty="{Binding SomeProperty}</c>".
    /// </summary>
    /// <typeparam name="T">The type of view model this control encapsulates.</typeparam>
    public class ParentUserControl<T> : UserControl where T : ViewModelBase
    {
        /// <inheritdoc cref="GlobalProvider.ServiceProvider" />
        protected IServiceProvider ServiceProvider => App.ServiceProvider;

        protected T ViewModel { get; }

        //The WPF designer will get upset if this constructor is protected
        public ParentUserControl()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();

            /* If MainWindow has a MainWindowViewModel containing SomeProperty, and tries to bind to a custom property
             * defined on a control that derives from this class by doing MyProperty="{Binding SomeProperty}", this won't work.
             * When a DataContext has not been explicitly set, WPF sets the DataContext to MainWindowViewModel in order to retrieve SomeProperty.
             * If an explicit DataContext has been set however, we won't receive MainWindowViewModel, and thus won't be able to receive SomeProperty */
            DataContext = ViewModel;
        }
    }
}
