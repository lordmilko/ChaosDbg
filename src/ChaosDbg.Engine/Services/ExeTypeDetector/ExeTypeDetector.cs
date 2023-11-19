using System;
using System.IO;
using System.Linq;
using ChaosLib;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface IExeTypeDetector
    {
        ExeKind Detect(string fileName);
    }

    public enum ExeKind
    {
        Native,
        NetFramework,
        NetCore
    }

    public class ExeTypeDetector : IExeTypeDetector
    {
        private readonly IPEFileProvider peFileProvider;
        private readonly ISigReader sigReader;

        public ExeTypeDetector(IPEFileProvider peFileProvider, ISigReader sigReader)
        {
            this.peFileProvider = peFileProvider;
            this.sigReader = sigReader;
        }

        public ExeKind Detect(string fileName)
        {
            //Is this a .NET executable? And if so what kind of .NET executable is it?

            //Does it have an IMAGE_COR20_HEADER?
            var pe = peFileProvider.ReadFile(fileName);

            if (pe.Cor20Header != null)
                return DetectDotnetType(fileName);

            if (pe.ImportDirectory != null && pe.ImportDirectory.Any(v => v.Name.Equals("mscoree.dll", StringComparison.OrdinalIgnoreCase)))
                return ExeKind.NetFramework;

            var directory = Path.GetDirectoryName(fileName);

            if (directory != null && Directory.EnumerateFiles(directory).Any(v => Path.GetFileName(v).Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)))
                return ExeKind.NetCore;

            //Didn't find any .NET indicators; assume native
            return ExeKind.Native;
        }

        private ExeKind DetectDotnetType(string fileName)
        {
            var disp = new MetaDataDispenserEx();
            var mdai = disp.OpenScope<MetaDataAssemblyImport>(fileName, CorOpenFlags.ofReadOnly);
            var mdi = mdai.As<MetaDataImport>();

            var assemblyAttribs = mdi.EnumCustomAttributes(mdai.AssemblyFromScope, mdToken.Nil);

            var typeRefResolver = new TypeRefResolver();
            var attribName = "System.Runtime.Versioning.TargetFrameworkAttribute";
            typeRefResolver.ShouldResolveAssemblyRef = (typeRef, assemblyRef, typeName) => typeName == attribName;

            foreach (var attrib in assemblyAttribs)
            {
                var result = sigReader.ReadCustomAttribute(attrib, mdi, typeRefResolver);

                if (result is SigCustomAttribute c && c.Name == attribName && c.FixedArgs.Length > 0)
                {
                    if ((c.FixedArgs[0] as SigCustomAttribFixedArg)?.Value is string version)
                    {
                        if (version.Contains("NETCoreApp"))
                            return ExeKind.NetCore;

                        if (version.Contains(".NETFramework"))
                            return ExeKind.NetFramework;
                    }
                }
            }

            //Couldn't find the attribute or a suitable value; assume it's .NET Framework
            return ExeKind.NetFramework;
        }
    }
}
