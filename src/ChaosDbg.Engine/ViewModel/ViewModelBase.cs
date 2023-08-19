using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ChaosDbg.ViewModel
{
    /// <summary>
    /// Represents a view model.<para/>
    /// Serves as the base class of all view models, and provides facilities for
    /// updating reactive values in a thread safe manner.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private SynchronizationContext context;

        protected object ReactiveLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        protected ViewModelBase()
        {
            //Store the current synchronization context
            context = SynchronizationContext.Current;
        }

        protected void SetProperty<T>(ref T field, ref T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            Action action = () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            //If we never had a context, or if we're on the same thread that we got our context from in the first place,
            //we can reasonably say that we're on the UI thread, so just invoke the event handler
            if (context == null || context == SynchronizationContext.Current)
                action();
            else
            {
                try
                {
                    //We're probably on a background thread, so post the event back to the UI thread.
                    //Use Send instead of post because it seems its possible for events to be processed out of order
                    context.Send(state => action(), null);
                }
                catch (InvalidAsynchronousStateException)
                {
                    //The application is probably closing
                }
            }
        }
    }
}