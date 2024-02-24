using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for self-extracting ChaosDbg when it has been compiled as a single file.
    /// </summary>
    static class SingleFileProvider
    {
        private static string UnpackDir = Path.Combine(AppContext.BaseDirectory, "lib");

        //This field is only accessed via reflection
        public static bool IsSingleFile { get; set; }

        internal static void ExtractChaosDbg()
        {
            var assembly = Assembly.GetEntryAssembly();
            var resources = assembly.GetManifestResourceNames();

            //If we didn't embed lib.zip, it's not a single file build
            if (!resources.Any(r => r == ".\\lib.zip"))
                return;

            IsSingleFile = true;

            foreach (var resource in resources)
                UnpackResource(assembly, resource);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static void UnpackResource(Assembly assembly, string resourceName)
        {
            var resourceStream = assembly.GetManifestResourceStream(resourceName);

            var ext = Path.GetExtension(resourceName);

            switch (ext)
            {
                case ".zip":
                    UnpackResourceZip(resourceStream);
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle resource with extension '{ext}'");
            }
        }

        private static void UnpackResourceZip(Stream resourceStream)
        {
            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                var outputPath = Path.GetFullPath(Path.Combine(UnpackDir, entry.FullName));

                var dir = Path.GetDirectoryName(outputPath);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(outputPath, true);
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name;

            var comma = name.IndexOf(',');

            if (comma != -1)
                name = name.Substring(0, comma);

            var fileName = Path.Combine(UnpackDir, $"{name}.dll");

            if (File.Exists(fileName))
                return Assembly.LoadFrom(fileName);

            return null;
        }
    }
}
