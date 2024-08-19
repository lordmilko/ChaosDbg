using System;

namespace ChaosDbg
{
    static class EventExtensions
    {
        /// <summary>
        /// Dispatches an event directly without concern for whether the event must be received by the UI thread.
        /// </summary>
        /// <typeparam name="T">The type of event args that should be passed to the event handler.</typeparam>
        /// <param name="handler">The event handler to be invoked.</param>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The event args to be passed to the event handler.</param>
        public static void HandleEvent<T>(EventHandler<T> handler, object sender, T args)
        {
            handler?.Invoke(handler, args);
        }
    }
}
