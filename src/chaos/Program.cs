using System.CommandLine;
using ChaosDbg;
using ChaosDbg.Metadata;

namespace chaos
{
    class Program
    {
        static void Main(string[] args)
        {
            var executableArgument = new Argument<string>("executable", "The command line of an executable process to launch and debug");

            var engineOption = new Option<DbgEngineKind?>(new[]{"-e", "--engine"}, "The debug engine to use");
            var minimizedOption = new Option<bool>("--minimized", "Whether to start the process minimized");

            var root = new RootCommand("ChaosDbg CLI")
            {
                executableArgument,
                engineOption,
                minimizedOption
            };
            root.SetHandler(
                (executable, engineKind, minimized) =>
                {
                    var kind = GetEngineKind(executable, engineKind);

                    switch (kind)
                    {
                        case DbgEngineKind.DbgEng:
                            GlobalProvider.ServiceProvider.GetService<DbgEngClient>().Execute(executable, minimized);
                            break;

                        case DbgEngineKind.Cordb:
                        case DbgEngineKind.Interop:
                            GlobalProvider.ServiceProvider.GetService<CordbClient>().Execute(executable, minimized, kind == DbgEngineKind.Interop);
                            break;

                        default:
                            throw new UnknownEnumValueException(kind);
                    }
                },
                executableArgument, engineOption, minimizedOption
            );

            root.Invoke(args);
        }

        private static DbgEngineKind GetEngineKind(string executable, DbgEngineKind? engineKind)
        {
            if (engineKind == null)
            {
                var detector = GlobalProvider.ServiceProvider.GetService<IExeTypeDetector>();

                var type = detector.Detect(executable);

                switch (type)
                {
                    case ExeKind.Native:
                        return DbgEngineKind.DbgEng;

                    case ExeKind.NetCore:
                    case ExeKind.NetFramework:
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
