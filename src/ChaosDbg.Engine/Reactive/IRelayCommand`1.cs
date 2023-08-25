using System.Windows.Input;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Defines an <see cref="ICommand"/> that relays events to user defined methods
    /// containing a single parameter of type <typeparamref name="T"/>.
    /// </summary>
    public interface IRelayCommand<in T> : IRelayCommand
    {
        bool CanExecute(T parameter);

        void Execute(T parameter);
    }
}
