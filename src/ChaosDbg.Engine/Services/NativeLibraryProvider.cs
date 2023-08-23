using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using ClrDebug;

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
    class NativeLibraryProvider : IDisposable
    {
        private string root;
        private bool disposed;

        private ConcurrentDictionary<string, IntPtr> loaded = new ConcurrentDictionary<string, IntPtr>();

        public NativeLibraryProvider()
        {
            root = Path.Combine(
                Path.GetDirectoryName(typeof(NativeLibraryProvider).Assembly.Location),
                IntPtr.Size == 4 ? "x86" : "x64"
            );

            NativeMethods.SetDllDirectory(root);
        }

        public IntPtr GetModuleHandle(string name)
        {
            return loaded.GetOrAdd(name, v =>
            {
                var path = Path.Combine(root, name);

                if (!File.Exists(path))
                    throw new NotImplementedException();

                var hModule = NativeMethods.LoadLibrary(path);

                if (hModule != IntPtr.Zero)
                    return hModule;

                var hr = (HRESULT)Marshal.GetHRForLastWin32Error();

                if (hr == HRESULT.ERROR_BAD_EXE_FORMAT)
                    throw new BadImageFormatException($"Failed to load module '{path}'. Module may target an architecture different from the current process.");

                var ex = Marshal.GetExceptionForHR((int)hr);

                throw new DllNotFoundException($"Unable to load DLL '{path}' or one of its dependencies: {ex.Message}");
            });
        }

        public T GetExport<T>(string moduleName, string procName)
        {
            var hModule = GetModuleHandle(moduleName);

            return GetExport<T>(hModule, procName);
        }

        public T GetExport<T>(IntPtr hModule, string name)
        {
            var result = NativeMethods.GetProcAddress(hModule, name);

            if (result != IntPtr.Zero)
                return Marshal.GetDelegateForFunctionPointer<T>(result);

            throw new EntryPointNotFoundException($"Unable to find entry point named '{name}' in DLL: {(HRESULT)Marshal.GetHRForLastWin32Error()}");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            foreach (var item in loaded)
                NativeMethods.FreeLibrary(item.Value);

            loaded.Clear();

            disposed = true;
        }
    }
}
