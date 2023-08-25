using System;
using System.Windows.Input;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Defines an <see cref="ICommand"/> that relays events to user defined methods
    /// containing a single parameter of type <typeparamref name="T"/>.
    /// </summary>
    public class RelayCommand<T> : IRelayCommand<T>
    {
        private Action<T> execute;
        private Func<T, bool> canExecute;

        public RelayCommand(Action<T> execute)
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            this.execute = execute;
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            if (canExecute == null)
                throw new ArgumentNullException(nameof(canExecute));

            this.execute = execute;
            this.canExecute = canExecute;
        }

        private T CastParameter(object parameter)
        {
            if (parameter == null && default(T) == null)
                return default(T);

            if (parameter is T arg)
                return arg;

            throw new InvalidOperationException($"Cannot use parameter '{parameter}' of type '{parameter?.GetType().FullName ?? "null"}' in a relay command expecting a value of type '{typeof(T).FullName}'.");
        }

        #region ICommand

        //A button may want to show as disabled when CanExecute() returns false. It subscribes to
        //CanExecuteChanged to be notified that it should requery CanExecute(). Normally CanExecuteChanged
        //is invoked by WPF internally, however we can force subscribers to requery CanExecute() by calling
        //RaiseCanExecuteChanged() below
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            //If default(T) is a value type, you can't specify null
            //as a parameter
            if (parameter == null && default(T) != null)
                return false;

            var value = CastParameter(parameter);

            return CanExecute(value);
        }

        public void Execute(object parameter)
        {
            var value = CastParameter(parameter);

            Execute(value);
        }

        #endregion
        #region IRelayCommand

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        #endregion
        #region IRelayCommand<T>

        public bool CanExecute(T parameter) => canExecute?.Invoke(parameter) != false;

        public void Execute(T parameter) => execute(parameter);

        #endregion
    }
}
