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
    public class NativeLibraryProvider : IDisposable
    {
        private string root;
        private bool disposed;

        private ConcurrentDictionary<string, Lazy<IntPtr>> loaded = new ConcurrentDictionary<string, Lazy<IntPtr>>();

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
            //GetOrAdd is not thread safe unless using Lazy<T>
            return loaded.GetOrAddSafe(name, () =>
            {
                var path = Path.Combine(root, name);

                if (!File.Exists(path))
                    throw new FileNotFoundException($"Failed to find module file '{path}'", path);

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

            //If we created any RCWs from a module we're about to unload,
            //ensure the finalizer runs before the module is unloaded
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (var item in loaded)
                NativeMethods.FreeLibrary(item.Value.Value);

            loaded.Clear();

            disposed = true;
        }
    }
}
