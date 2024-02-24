using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ChaosLib;

namespace ChaosDbg
{
    static class WellKnownNativeLibrary
    {
        public const string DbgEng = "dbgeng.dll";
        public const string DbgHelp = "dbghelp.dll";
    }

    /// <summary>
    /// Provides facilities for locating and interacting with native libraries in the output directory of ChaosDbg.
    /// </summary>
    public class NativeLibraryProvider : IDisposable
    {
        private string root;
        private bool disposed;

        private ConcurrentDictionary<string, Lazy<IntPtr>> loaded = new ConcurrentDictionary<string, Lazy<IntPtr>>();

        public NativeLibraryProvider()
        {
            string baseDir;

            if (!TryGetSingleFileBase(out baseDir))
                baseDir = AppContext.BaseDirectory;

            root = Path.Combine(
                baseDir,
                IntPtr.Size == 4 ? "x86" : "x64"
            );

            Kernel32.SetDllDirectory(root);
        }

        private bool TryGetSingleFileBase(out string baseDir)
        {
            baseDir = null;

            var assembly = Assembly.GetEntryAssembly();

            var singleFileProviderType = assembly.GetType("ChaosDbg.SingleFileProvider");

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

        public IntPtr GetModuleHandle(string name)
        {
            //GetOrAdd is not thread safe unless using Lazy<T>
            return loaded.GetOrAddSafe(name, () =>
            {
                var path = GetModuleRoot(name);

                if (!File.Exists(path))
                    throw new FileNotFoundException($"Failed to find module file '{path}'", path);

                var hModule = Kernel32.LoadLibrary(path);

                return hModule;
            });
        }

        public T GetExport<T>(string moduleName, string procName)
        {
            var hModule = GetModuleHandle(moduleName);

            return GetExport<T>(hModule, procName);
        }

        public T GetExport<T>(IntPtr hModule, string name)
        {
            var result = Kernel32.GetProcAddress(hModule, name);

            return Marshal.GetDelegateForFunctionPointer<T>(result);
        }

        private string GetModuleRoot(string moduleName)
        {
#if DEBUG
            switch (moduleName)
            {
                case WellKnownNativeLibrary.DbgEng:
                case WellKnownNativeLibrary.DbgHelp:
                    if (DbgEngResolver.TryGetDbgEngPath(out var path))
                        return Path.Combine(path, moduleName);
                    break;
            }
#endif

            return Path.Combine(root, moduleName);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            //If we created any RCWs from a module we're about to unload,
            //ensure the finalizer runs before the module is unloaded
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (var item in loaded)
                Kernel32.FreeLibrary(item.Value.Value);

            loaded.Clear();

            disposed = true;
        }
    }
}
