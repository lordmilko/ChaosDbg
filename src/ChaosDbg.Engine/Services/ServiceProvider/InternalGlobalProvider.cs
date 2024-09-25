using System;
using System.ComponentModel;
using System.Diagnostics;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.DbgEng.Server;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosDbg.Symbols;
using ChaosLib;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;

namespace ChaosDbg
{
    /// <summary>
    /// Provides the global <see cref="IServiceProvider"/> used by the application.<para/>
    /// This type should not be used directly. An application specific GlobalProvider should be used instead.
    /// </summary>
    [Obsolete("Do not use this type directly. Use an application specific GlobalProvider instead.")]
    internal class InternalGlobalProvider
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

        static InternalGlobalProvider()
        {
            //If a test is trying to re-initialize the service provider, it better be null already
            Debug.Assert(serviceProvider == null);
            serviceProvider = new Lazy<IServiceProvider>(() => CreateServiceProvider(ConfigureServices));
        }

        internal static IServiceProvider CreateServiceProvider(Action<ServiceCollection> configureServices)
        {
            var services = new ServiceCollection
            {
                //Debug Engines
                typeof(DebugEngineProvider),

                //Debug Engine Service Collections
                typeof(CordbEngineServices),
                typeof(DbgEngEngineServices),

                //Symbols

                //NativeLibrary
                { typeof(INativeLibraryProvider), typeof(NativeLibraryProvider) },
                { typeof(INativeLibraryBaseDirectoryProvider), typeof(SingleFileNativeLibraryBaseDirectoryProvider) },
                { typeof(INativeLibraryLoadCallback[]), new[]
                {
                    typeof(DbgEngNativeLibraryLoadCallback),
                    typeof(DbgHelpNativeLibraryLoadCallback),
                    typeof(MSDiaNativeLibraryLoadCallback),
                    typeof(SymSrvNativeLibraryLoadCallback)
                }},

                typeof(CordbMasmEvaluatorContext),

                //Misc

                typeof(DbgEngRemoteClientProvider),

                { typeof(IFrameworkTypeDetector), typeof(FrameworkTypeDetector) }
            };

            configureServices?.Invoke(services);

            var provider = services.Build();

            return provider;
        }

        public static void Dispose()
        {
            Log.Debug<InternalGlobalProvider>("Disposing service provider");

            Debug.Assert(serviceProvider != null, "Service provider is either being double disposed, another thread disposed it underneath us, or after disposing it a test did not reinitialize it");

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
