using System;
using System.Windows.Input;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Specifies that a method should have a <see cref="IRelayCommand"/> property automatically generated for it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RelayCommandAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the method that should be used for evaluating <see cref="ICommand.CanExecute(object)"/> in the command handler.
        /// </summary>
        public string CanExecute { get; set; }
    }
}
