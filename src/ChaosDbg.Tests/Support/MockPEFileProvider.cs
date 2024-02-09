using System;
using System.IO;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.Tests
{
    class MockPEFileProvider : PEFileProvider
    {
        private PEFileProvider peProvider = new PEFileProvider();

        public Action<MockRelayPEFile> ConfigureMock { get; set; }

        public override IPEFile ReadStream(Stream stream, bool isLoadedImage, PEFileDirectoryFlags flags = PEFileDirectoryFlags.None, IPESymbolResolver symbolResolver = null)
        {
            var realFile = peProvider.ReadStream(stream, isLoadedImage, flags, symbolResolver);

            if (ConfigureMock == null)
                return realFile;

            var mockFile = new MockRelayPEFile(realFile);

            ConfigureMock(mockFile);

            return mockFile;
        }

        public override IPEFile ReadFile(string path, PEFileDirectoryFlags flags = PEFileDirectoryFlags.None, IPESymbolResolver symbolResolver = null)
        {
            var realFile = peProvider.ReadFile(path, flags, symbolResolver);

            if (ConfigureMock == null)
                return realFile;

            var mockFile = new MockRelayPEFile(realFile);

            ConfigureMock(mockFile);

            return mockFile;
        }
    }
}
