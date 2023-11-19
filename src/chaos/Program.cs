using System;
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

            var root = new RootCommand("ChaosDbg CLI")
            {
                executableArgument,
                engineOption
            };
            root.SetHandler((executable, engineKind) =>
            {
                var kind = GetEngineKind(executable, engineKind);

                switch (kind)
                {
                    case DbgEngineKind.DbgEng:
                        new DbgEngClient().Execute(executable);
                        break;

                    case DbgEngineKind.Cordb:
                        new CordbClient().Execute(executable);
                        break;

                    default:
                        throw new UnknownEnumValueException(kind);
                }
            }, executableArgument, engineOption);

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
