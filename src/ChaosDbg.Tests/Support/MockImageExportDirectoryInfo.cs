using ChaosLib.PortableExecutable;

namespace ChaosDbg.Tests
{
    class MockImageExportDirectoryInfo : IImageExportDirectoryInfo
    {
        public int Characteristics => exportInfo.Characteristics;

        public int TimeDateStamp => exportInfo.TimeDateStamp;

        public ushort MajorVersion => exportInfo.MajorVersion;

        public ushort MinorVersion => exportInfo.MinorVersion;

        public string Name => exportInfo.Name;

        public int Base => exportInfo.Base;

        public IImageExportInfo[] Exports => exports;

        private IImageExportDirectoryInfo exportInfo;
        private IImageExportInfo[] exports;

        internal MockImageExportDirectoryInfo(IImageExportDirectoryInfo exportInfo, IImageExportInfo[] exports)
        {
            this.exportInfo = exportInfo;
            this.exports = exports;
        }
    }
}
