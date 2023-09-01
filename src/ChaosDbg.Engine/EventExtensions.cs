using System;
using System.Windows;

namespace ChaosDbg
{
    static class EventExtensions
    {
        /// <summary>
        /// Dispatches an event that is intended for the UI using the current application dispatcher if it is available.<para/>
        /// If no dispatcher is available, the event is invoked using the current thread.
        /// </summary>
        /// <typeparam name="T">The type of event args that should be passed to the event handler.</typeparam>
        /// <param name="handler">The event handler to be invoked.</param>
        /// <param name="args">The event args to be passed to the event handler.</param>
        public static void HandleUIEvent<T>(EventHandler<T> handler, T args)
        {
            if (handler != null)
            {
                var dispatcher = Application.Current?.Dispatcher;

                //Asynchronously dispatch to the UI thread so that if we're on the engine thread, we don't deadlock
                //if the event handler on UI thread tries to invoke a command back on the engine thread
                if (dispatcher != null)
                    dispatcher.InvokeAsync(() => handler.Invoke(handler, args));
                else
                    handler.Invoke(handler, args);
            }
        }

        public static void HandleSimpleEvent<T>(EventHandler<T> handler, T args)
        {
            handler?.Invoke(handler, args);
        }
    }
}
