using System.CommandLine;
using ChaosDbg;
using ChaosDbg.Metadata;

namespace chaos
{
    class Program
    {
        static void Main(string[] args)
        {
            //In single file builds, a zip file is embedded in the exe containing all managed/unmanaged assemblies,
            //making ChaosDbg easily portable as well as reducing the overall file size
            SingleFileProvider.ExtractChaosDbg();

            Run(args);
        }

        private static void Run(string[] args)
        {
            var executableArgument = new Argument<string>("executable", "The command line of an executable process to launch and debug");

            var engineOption = new Option<DbgEngineKind?>(new[]{"-e", "--engine"}, "The debug engine to use");
            var minimizedOption = new Option<bool>("--minimized", "Whether to start the process minimized");
            var frameworkKindOption = new Option<FrameworkKind?>(new[]{"-k", "--frameworkKind" }, "The kind of .NET runtime this EXE launches (if applicable)");

            var root = new RootCommand("ChaosDbg CLI")
            {
                executableArgument,
                engineOption,
                minimizedOption,
                frameworkKindOption
            };
            root.SetHandler(
                (executable, engineKind, minimized, frameworkKind) =>
                {
                    var kind = GetEngineKind(executable, engineKind);

                    switch (kind)
                    {
                        case DbgEngineKind.DbgEng:
                            GlobalProvider.ServiceProvider.GetService<DbgEngClient>().Execute(executable, minimized);
                            break;

                        case DbgEngineKind.Cordb:
                        case DbgEngineKind.Interop:
                            GlobalProvider.ServiceProvider.GetService<CordbClient>().Execute(executable, minimized, kind == DbgEngineKind.Interop, frameworkKind);
                            break;

                        default:
                            throw new UnknownEnumValueException(kind);
                    }
                },
                executableArgument, engineOption, minimizedOption, frameworkKindOption
            );

            root.Invoke(args);
        }

        private static DbgEngineKind GetEngineKind(string executable, DbgEngineKind? engineKind)
        {
            if (engineKind == null)
            {
                var detector = GlobalProvider.ServiceProvider.GetService<IFrameworkTypeDetector>();

                var type = detector.Detect(executable);

                switch (type)
                {
                    case FrameworkKind.Native:
                        return DbgEngineKind.DbgEng;

                    case FrameworkKind.NetCore:
                    case FrameworkKind.NetFramework:
                        return DbgEngineKind.Cordb;

                    default:
                        throw new UnknownEnumValueException(type);
                }
            }
            else
                return engineKind.Value;
        }
    }
}
