using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    public static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider serviceProvider) => (T) serviceProvider.GetService(typeof(T));

        /// <summary>
        /// Creates a new dynamic view model proxy of type <typeparamref name="T"/> containing any services
        /// that must be resolved from an <see cref="IServiceProvider"/>.
        /// </summary>
        /// <typeparam name="T">The type of view model proxy to create.</typeparam>
        /// <param name="serviceProvider">The service provider to use to resolve dependencies required by the view model.</param>
        /// <returns>A view model proxy of type <typeparamref name="T"/>.</returns>
        public static T GetViewModel<T>(this IServiceProvider serviceProvider) where T : INotifyPropertyChanged
        {
            if (!typeof(ViewModelBase).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException($"Type '{typeof(T).Name}' must derive from '{nameof(ViewModelBase)}'.");

            var ctor = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();

            var args = new List<object>();

            foreach (var parameter in ctor.GetParameters())
            {
                var value = serviceProvider.GetService(parameter.ParameterType);

                args.Add(value);
            }

            var viewModel = ViewModelProvider.Create<T>(args.ToArray());

            return viewModel;
        }
    }
}