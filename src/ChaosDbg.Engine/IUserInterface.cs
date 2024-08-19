using System;

namespace ChaosDbg
{
    public interface IUserInterface
    {
        /// <summary>
        /// Dispatches an event that is intended for the UI using the current application dispatcher if it is available.<para/>
        /// If no dispatcher is available, the event is invoked using the current thread.
        /// </summary>
        /// <typeparam name="T">The type of event args that should be passed to the event handler.</typeparam>
        /// <param name="handler">The event handler to be invoked.</param>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The event args to be passed to the event handler.</param>
        void HandleEvent<T>(EventHandler<T> handler, object sender, T args);
    }
}
