using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

namespace ChaosDbg
{
    /// <summary>
    /// Represents resources that are specific to an <see cref="ChaosDbg.App"/> running under a specific <see cref="Dispatcher"/>.
    /// </summary>
    internal class GlobalAppServices
    {
        private static ConditionalWeakTable<Dispatcher, GlobalAppServices> servicesMap = new();

        internal static void Add(App app, IServiceProvider serviceProvider)
        {
            servicesMap.Add(app.Dispatcher, new GlobalAppServices
            {
                App = app,
                ServiceProvider = serviceProvider
            });
        }

        internal static void Remove(App app)
        {
            servicesMap.Remove(app.Dispatcher);
        }

        internal static GlobalAppServices Current
        {
            get
            {
                var currentDispatcher = Dispatcher.FromThread(Thread.CurrentThread);

                if (currentDispatcher == null)
                {
                    var message = "The current thread does not have a Dispatcher associated with it";
                    Debug.Assert(false, message);
                    throw new InvalidOperationException(message);
                }

                if (servicesMap.TryGetValue(currentDispatcher, out var serviceProvider))
                    return serviceProvider;

                throw new InvalidOperationException($"Could not find any {nameof(GlobalAppServices)} associated with the current thread");
            }
        }

        public App App { get; private set; }

        public IServiceProvider ServiceProvider { get; private set; }
    }
}
