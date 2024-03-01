using System;
using System.ComponentModel;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;

namespace chaos
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
                typeof(DbgEngClient),
                typeof(CordbClient),
                typeof(CommandBuilder),

                typeof(DbgEngEngineProvider),
                typeof(CordbEngineProvider),
                typeof(NativeLibraryProvider),

                typeof(CordbEngineServices),
                typeof(DbgEngEngineServices),

                typeof(CordbMasmEvaluatorContext),
                typeof(ILDisassemblerProvider),

                typeof(ConsoleDisasmWriter),
                { typeof(IConsole), typeof(PhysicalConsole) },

                { typeof(IExeTypeDetector), typeof(ExeTypeDetector) },
                { typeof(IPEFileProvider), typeof(PEFileProvider) },
                { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) },
                { typeof(ISigReader), typeof(SigReader) }
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

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            serviceProvider = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }
    }
}
