using System;
using System.IO;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.Tests
{
    class MockPEFileProvider : PEFileProvider
    {
        public Action<PEFile> ConfigureMock { get; set; }

        public override PEFile ReadStream(Stream stream, bool isLoadedImage, PEFileDirectoryFlags flags = PEFileDirectoryFlags.None, IPESymbolResolver symbolResolver = null)
        {
            var realFile = Instance.ReadStream(stream, isLoadedImage, flags, symbolResolver);

            if (ConfigureMock == null)
                return realFile;

            ConfigureMock(realFile);

            return realFile;
        }

        public override PEFile ReadFile(string path, PEFileDirectoryFlags flags = PEFileDirectoryFlags.None, IPESymbolResolver symbolResolver = null)
        {
            var realFile = Instance.ReadFile(path, flags, symbolResolver);

            if (ConfigureMock == null)
                return realFile;

            ConfigureMock(realFile);

            return realFile;
        }
    }
}
