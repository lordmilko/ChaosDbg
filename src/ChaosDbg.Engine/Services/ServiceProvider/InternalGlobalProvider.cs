using System;
using System.ComponentModel;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.DbgEng.Server;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.IL;
using ChaosDbg.Metadata;
using ChaosLib;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;

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

        //When running unit tests, in order to enable running multiple tests at once, we will store a unique global service provider
        //on each thread. The only place the global provider should be used is on application entry points, so I don't think there should
        //be any issues with multiple threads trying to access the same global provider. If that is the case, that means we have a a design
        //issue and should fix it
        [ThreadStatic]
        internal static IServiceProvider TestServiceProvider;

        /// <summary>
        /// Gets the global <see cref="IServiceProvider"/> of the application.
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                    throw new ObjectDisposedException(nameof(ServiceProvider));

                if (TestServiceProvider != null)
                    return TestServiceProvider;

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
            serviceProvider = new Lazy<IServiceProvider>(CreateServiceProvider);
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection
            {
                //Debug Engines
                typeof(DbgEngEngineProvider),
                typeof(CordbEngineProvider),

                //Debug Engine Service Collections
                typeof(CordbEngineServices),
                typeof(DbgEngEngineServices),

                //Symbols
                typeof(SymHelp),
                typeof(MicrosoftPdbSourceFileProvider),

                //NativeLibrary
                typeof(NativeLibraryProvider),
                { typeof(INativeLibraryBaseDirectoryProvider), typeof(SingleFileNativeLibraryBaseDirectoryProvider) },
                { typeof(INativeLibraryLoadCallback[]), new[]
                {
                    typeof(DbgEngNativeLibraryLoadCallback),
                    typeof(DbgHelpNativeLibraryLoadCallback),
                    typeof(MSDiaNativeLibraryLoadCallback),
                    typeof(SymSrvNativeLibraryLoadCallback)
                }},

                typeof(CordbMasmEvaluatorContext),
                typeof(ILDisassemblerProvider),

                //Misc

                typeof(DbgEngRemoteClientProvider),

                { typeof(IFrameworkTypeDetector), typeof(FrameworkTypeDetector) },
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
