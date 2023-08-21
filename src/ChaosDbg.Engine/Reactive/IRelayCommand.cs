using System.Windows.Input;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Defines an <see cref="ICommand"/> that relays events to user defined methods.
    /// </summary>
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }
}
