using System;
using System.ComponentModel;
using ChaosDbg.Engine;
using ChaosDbg.Text;
using ChaosDbg.Theme;

namespace ChaosDbg
{
    /// <summary>
    /// Provides the global <see cref="IServiceProvider"/> used by the application.
    /// </summary>
    public class GlobalProvider
    {
        //The service probider is lazily loaded on first access
        private static Lazy<IServiceProvider> serviceProvider;

        /// <summary>
        /// Gets the global <see cref="IServiceProvider"/> of the application.
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                    throw new ObjectDisposedException(nameof(ServiceProvider));

                return serviceProvider.Value;
            }
        }

        /// <summary>
        /// An optional hook that allows modifying services.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Action<ServiceCollection> ConfigureServices { get; set; }

        static GlobalProvider()
        {
            serviceProvider = new Lazy<IServiceProvider>(CreateServiceProvider);
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection
            {
                { typeof(IThemeProvider), typeof(ThemeProvider) },
                { typeof(ITextBufferProvider), typeof(TextBufferProvider) }
            };

            ConfigureServices?.Invoke(services);

            var provider = services.Build();

            return provider;
        }

        public static void Dispose()
        {
            if (serviceProvider.IsValueCreated)
            {
                if (serviceProvider.Value is IDisposable d)
                    d.Dispose();
            }

            serviceProvider = null;
        }
    }
}
