using System;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.Metadata;
using ChaosLib.Metadata;

namespace ChaosDbg.Tests
{
    public abstract class BaseTest
    {
        private IServiceProvider serviceProvider;

        private IServiceProvider ServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                {
                    var serviceCollection = new ServiceCollection
                    {
                        typeof(DbgEngEngine),
                        typeof(NativeLibraryProvider),

                        { typeof(IExeTypeDetector), typeof(ExeTypeDetector) },
                        { typeof(ICordbEngine), typeof(CordbEngine) },
                        { typeof(IPEFileProvider), typeof(PEFileProvider) },
                        { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) },
                        { typeof(ISigReader), typeof(SigReader) }
                    };

                    serviceProvider = serviceCollection.Build();
                }

                return serviceProvider;
            }
        }

        protected T GetService<T>() => ServiceProvider.GetService<T>();
    }
}
