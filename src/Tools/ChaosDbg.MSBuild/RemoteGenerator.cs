using System;
using System.IO;
using System.Reflection;

namespace ChaosDbg.MSBuild
{
    /// <summary>
    /// Generates source generated files in a separate <see cref="AppDomain"/>.
    /// </summary>
    class RemoteGenerator : MarshalByRefObject
    {
        public string[] ExecuteRemote(string[] files, string output, string kind)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            switch (kind)
            {
                case nameof(GenerateViewModels):
                    return new ViewModelGenerator().Generate(files, output);

                case nameof(GenerateDependencyProperties):
                    return new DependencyPropertyGenerator().Generate(files, output);

                default:
                    throw new NotImplementedException($"Don't know how to handle generator task of type '{kind}'.");
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name;

            if (args.Name != null && args.Name.Contains(","))
                name = name.Substring(0, name.IndexOf(','));

            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(GetType().Assembly.Location), "*.dll"))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(file), name))
                    return Assembly.LoadFile(file);
            }

            return null;
        }

        public override object InitializeLifetimeService() => null;
    }
}
