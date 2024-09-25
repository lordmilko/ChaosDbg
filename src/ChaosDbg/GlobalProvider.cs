using System;
using System.ComponentModel;
using System.Diagnostics;
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
        internal static bool AllowGlobalProvider { get; set; } = true;

        /// <summary>
        /// Gets the global <see cref="IServiceProvider"/> of the application.
        /// </summary>
#pragma warning disable CS0618
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (!AllowGlobalProvider)
                    Debug.Assert(false, "The GlobalProvider is not allowed to be used in this context. You must pass the IServiceProvider to this context directly.");

                return InternalGlobalProvider.ServiceProvider;
            }
        }
#pragma warning restore CS0618

        /// <summary>
        /// An optional hook that allows modifying services.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Action<ServiceCollection> ConfigureServices { get; set; }

        static GlobalProvider()
        {
#pragma warning disable CS0618
            InternalGlobalProvider.ConfigureServices = services =>
#pragma warning restore CS0618
            {
                var appServices = new ServiceCollection
                {
                    { typeof(IThemeProvider), typeof(ThemeProvider) },
                    { typeof(ITextBufferProvider), typeof(TextBufferProvider) }
                };

                foreach (var serviceEntry in appServices)
                    services.Add(serviceEntry);

                ConfigureServices?.Invoke(services);
            };
        }

        internal static IServiceProvider CreateServiceProvider(Action<ServiceCollection> configureServices)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return InternalGlobalProvider.CreateServiceProvider(services =>
            {
                //We set InternalGlobalProvider.ConfigureServices, so we need to ensure that we invoke that
                Debug.Assert(InternalGlobalProvider.ConfigureServices != null);
                InternalGlobalProvider.ConfigureServices(services);

                configureServices?.Invoke(services);
            });
#pragma warning restore CS0618 // Type or member is obsolete
        }

#pragma warning disable CS0618
        public static void Dispose() => InternalGlobalProvider.Dispose();
#pragma warning restore CS0618
    }
}
