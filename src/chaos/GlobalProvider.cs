using System;
using System.ComponentModel;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Engine;
using ChaosDbg.Terminal;

namespace chaos
{
    /// <summary>
    /// Provides the global <see cref="IServiceProvider"/> used by the application.
    /// </summary>
    public class GlobalProvider
    {
        /// <summary>
        /// Gets the global <see cref="IServiceProvider"/> of the application.
        /// </summary>
#pragma warning disable CS0618
        public static IServiceProvider ServiceProvider => InternalGlobalProvider.ServiceProvider;
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
                    //chaos
                    typeof(DbgEngClient),
                    typeof(CordbClient),
                    typeof(CommandBuilder),
                    { typeof(ITerminal), typeof(ConsoleTerminal) },

                    //Console
                    typeof(ConsoleDisasmWriter),
                    { typeof(IConsole), typeof(PhysicalConsole) }
                };

                foreach (var serviceEntry in appServices)
                    services.Add(serviceEntry);

                ConfigureServices?.Invoke(services);
            };
        }

#pragma warning disable CS0618
        public static void Dispose() => InternalGlobalProvider.Dispose();
#pragma warning restore CS0618
    }
}
