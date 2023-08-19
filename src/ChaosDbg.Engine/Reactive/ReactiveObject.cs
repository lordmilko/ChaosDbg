using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Represents a reactive object.<para/>
    /// Serves as the base class of general purpose objects that require the <see cref="INotifyPropertyChanged"/> pattern,
    /// including dynamic reactive proxies.
    /// </summary>
    public abstract class ReactiveObject : INotifyPropertyChanged
    {
        private static ConcurrentDictionary<Type, ReactiveProxyInfo> proxyCache = new ConcurrentDictionary<Type, ReactiveProxyInfo>();
        private static object objLock = new object();

        /// <summary>
        /// Creates a new instance of a reactive proxy around type <typeparamref name="T"/>,
        /// creating a new proxy type if required, or using the existing type if one has been dynamically generated previously.
        /// </summary>
        /// <typeparam name="T">The type to create a reactive proxy for.</typeparam>
        /// <param name="args">The arguments that will be passed down through the reactive proxy to the constructor of type <typeparamref name="T"/>.</param>
        /// <returns>A reactive proxy that derives from type <typeparamref name="T"/>.</returns>
        public static T New<T>(params object[] args) where T : ReactiveObject
        {
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetProperty<T>(ref T field, ref T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}