﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.Engine
{
    public class ServiceCollection : IEnumerable<ServiceDescriptor>
    {
        private readonly Dictionary<Type, ServiceDescriptor> services = new Dictionary<Type, ServiceDescriptor>();

        public void AddSingleton(Type serviceType, Type implementationType)
        {
            Validate(serviceType, implementationType);

            services[serviceType] = new ServiceDescriptor(serviceType, implementationType);
        }

        public void Add(Type serviceType) => AddSingleton(serviceType, serviceType);

        public void Add(Type serviceType, Type implementationType) => AddSingleton(serviceType, implementationType);

        public void Add<T>(Func<IServiceProvider, T> factory) where T : class
        {
            var type = factory.Method.ReturnType;

            Validate(type, null);

            services[type] = new ServiceDescriptor(type, type, factory: factory);
        }

        public void Add(Type serviceType, Type[] implementationTypes)
        {
            if (!serviceType.IsArray)
                throw new NotImplementedException();

            Validate(serviceType, null);

            services[serviceType] = new ServiceDescriptor(serviceType, serviceType, factory: s =>
            {
                var array = Array.CreateInstance(serviceType.GetElementType(), implementationTypes.Length);

                for (var i = 0; i < implementationTypes.Length; i++)
                {
                    var element = ((ServiceProvider) s).ResolveArrayService(implementationTypes[i]);

                    ((ServiceProvider) s).AddSingleton(element.GetType(), element);

                    array.SetValue(element, i);
                }

                return array;
            });
        }

        public void Add(Type serviceType, object implementation)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            if (!serviceType.IsInstanceOfType(implementation))
                throw new ArgumentException($"Implementation type '{implementation.GetType().Name}' does not implement service type '{serviceType.Name}'");

            Validate(serviceType, implementation.GetType());

            services[serviceType] = new ServiceDescriptor(serviceType, implementation.GetType(), implementation: implementation);
        }

        public void Add(ServiceDescriptor serviceDescriptor)
        {
            if (services.TryGetValue(serviceDescriptor.ServiceType, out _))
                throw new InvalidOperationException($"Cannot create service '{serviceDescriptor.ServiceType.Name}': service has already been added to the {nameof(ServiceCollection)}.");

            services.Add(serviceDescriptor.ServiceType, serviceDescriptor);
        }

        public void Replace(Type serviceType, Type newImplementationType)
        {
            services.Remove(serviceType);
            Add(serviceType, newImplementationType);
        }

        public void Replace<T>(Func<IServiceProvider, T> newFactory) where T : class
        {
            services.Remove(typeof(T));
            Add(newFactory);
        }

        public void Replace(Type serviceType, object newImplementation)
        {
            services.Remove(serviceType);
            Add(serviceType, newImplementation);
        }

        private void Validate(Type serviceType, Type implementationType)
        {
            if (implementationType != null && implementationType.IsInterface)
                throw new ArgumentException($"Cannot create service using implementation type '{implementationType.Name}': type is an interface.", nameof(implementationType));

            if (services.TryGetValue(serviceType, out _))
                throw new InvalidOperationException($"Cannot create service '{serviceType.Name}': service has already been added to the {nameof(ServiceCollection)}.");
        }

        public IServiceProvider Build()
        {
            var serviceProvider = new ServiceProvider(services.Values.ToArray());

            return serviceProvider;
        }

        public IEnumerator<ServiceDescriptor> GetEnumerator() => services.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
