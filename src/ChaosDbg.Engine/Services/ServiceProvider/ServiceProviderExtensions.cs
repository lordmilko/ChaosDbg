﻿using System;

namespace ChaosDbg
{
    public static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider serviceProvider) => (T) serviceProvider.GetService(typeof(T));
    }
}
