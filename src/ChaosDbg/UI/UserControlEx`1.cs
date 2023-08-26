using System;
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

    public class UserControlEx<T> : UserControl where T : ViewModelBase
    {
        protected IServiceProvider ServiceProvider => GlobalProvider.ServiceProvider;

        protected T ViewModel { get; }

        //The WPF designer will get upset if this constructor is protected
        public UserControlEx()
        {
            ViewModel = ServiceProvider.GetViewModel<T>();
            DataContext = ViewModel;
        }
    }
}
