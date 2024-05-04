using System;
using System.Reflection;
using ChaosLib;

namespace ChaosDbg
{
    class SingleFileNativeLibraryBaseDirectoryProvider : INativeLibraryBaseDirectoryProvider
    {
        public bool TryGetBaseDirectory(out string baseDir)
        {
            baseDir = null;

            var assembly = Assembly.GetEntryAssembly();

            var singleFileProviderType = assembly?.GetType("ChaosDbg.SingleFileProvider");

            if (singleFileProviderType == null)
                return false;

            var isSingleFileInfo = singleFileProviderType.GetProperty("IsSingleFile");

            if (isSingleFileInfo == null)
                throw new MissingMemberException(singleFileProviderType.Name, "IsSingleFile");

            var isSingleFile = (bool) isSingleFileInfo.GetValue(null);

            if (!isSingleFile)
                return false;

            var unpackDirInfo = singleFileProviderType.GetField("UnpackDir", BindingFlags.Static | BindingFlags.NonPublic);

            if (unpackDirInfo == null)
                throw new MissingMemberException(singleFileProviderType.Name, "UnpackDir");

            baseDir = (string) unpackDirInfo.GetValue(null);
            return true;
        }
    }
}
