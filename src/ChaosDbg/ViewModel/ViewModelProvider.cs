using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using ChaosDbg.Reactive;

namespace ChaosDbg.ViewModel
{
    /// <summary>
    /// Provides facilities for creating reactive proxies around view models.
    /// </summary>
    public class ViewModelProvider
    {
        private static ConcurrentDictionary<Type, ReactiveProxyInfo> proxyCache = new ConcurrentDictionary<Type, ReactiveProxyInfo>();
        private static object objLock = new object();

        /// <summary>
        /// Creates a new instance of a reactive view model proxy around type <typeparamref name="T"/>,
        /// creating a new proxy type if required, or using the existing type if one has been dynamically generated previously.
        /// </summary>
        /// <typeparam name="T">The type to create a reactive view model for.</typeparam>
        /// <param name="args">The arguments that will be passed down through the reactive proxy to the constructor of type <typeparamref name="T"/>.</param>
        /// <returns>A reactive view model proxy that derives from type <typeparamref name="T"/>.</returns>
        public static T Create<T>(params object[] args) where T : INotifyPropertyChanged
        {
            if (!typeof(ViewModelBase).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException($"Type '{typeof(T).Name}' must derive from '{nameof(ViewModelBase)}'.");

            lock (objLock)
            {
                if (!proxyCache.TryGetValue(typeof(T), out var proxy))
                {
                    proxy = ReactiveProxyBuilder.Build(typeof(T), args);
                    proxyCache[typeof(T)] = proxy;
                }

                return proxy.CreateInstance<T>(args);
            }
        }
    }
}
