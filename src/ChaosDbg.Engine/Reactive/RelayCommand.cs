using System;
using System.Windows.Input;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Defines an <see cref="ICommand"/> that relays events to user defined methods.
    /// </summary>
    public class RelayCommand : IRelayCommand
    {
        private Action execute;
        private Func<bool> canExecute;

        public RelayCommand(Action execute)
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            this.execute = execute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            if (canExecute == null)
                throw new ArgumentNullException(nameof(canExecute));

            this.execute = execute;
            this.canExecute = canExecute;
        }

        #region ICommand

        //A button may want to show as disabled when CanExecute() returns false. It subscribes to
        //CanExecuteChanged to be notified that it should requery CanExecute(). Normally CanExecuteChanged
        //is invoked by WPF internally, however we can force subscribers to requery CanExecute() by calling
        //RaiseCanExecuteChanged() below
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => canExecute?.Invoke() != false;

        public void Execute(object parameter) => execute();

        #endregion
        #region IRelayCommand

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
