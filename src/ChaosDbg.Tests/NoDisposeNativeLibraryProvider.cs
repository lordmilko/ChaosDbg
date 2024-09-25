using System;
using ChaosLib;

namespace ChaosDbg.Tests
{
    class NoDisposeNativeLibraryProvider : INativeLibraryProvider
    {
        public static readonly INativeLibraryProvider Instance = new NoDisposeNativeLibraryProvider();

        public IntPtr GetModuleHandle(string name) => ServiceSingletons.NativeLibraryProvider.GetModuleHandle(name);

        public T GetExport<T>(string moduleName, string procName) => ServiceSingletons.NativeLibraryProvider.GetExport<T>(moduleName, procName);

        public T GetExport<T>(IntPtr hModule, string name) => ServiceSingletons.NativeLibraryProvider.GetExport<T>(hModule, name);
    }
}
