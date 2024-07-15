using System.Threading;
using ChaosDbg.DbgEng;
using ChaosDbg.DbgEng.Server;

namespace ChaosDbg
{
    public partial class DebugEngineProvider
    {
        public class DbgEngEngineLaunchExtensions
        {
            private DebugEngineProvider debugEngineProvider;

            public DbgEngEngineLaunchExtensions(DebugEngineProvider debugEngineProvider)
            {
                this.debugEngineProvider = debugEngineProvider;
            }

            public DbgEngEngine CreateProcess(
                string commandLine,
                bool startMinimized = false,
                bool initialBreak = true,
                bool debugChildProcesses = false,
                CancellationToken cancellationToken = default)
            {
                return (DbgEngEngine) debugEngineProvider.CreateProcess(
                    DbgEngineKind.DbgEng,
                    new CreateProcessTargetOptions(commandLine)
                    {
                        StartMinimized = startMinimized,
                        InitialBreak = initialBreak,
                        DebugChildProcesses = debugChildProcesses
                    },
                    cancellationToken
                );
            }

            public DbgEngEngine Attach(
                int processId,
                bool nonInvasive = false,
                bool noSuspend = false,
                bool useDbgEngSymOpts = true,
                int? dbgEngEngineId = null,
                CancellationToken cancellationToken = default)
            {
                return (DbgEngEngine) debugEngineProvider.Attach(
                    DbgEngineKind.DbgEng,
                    new AttachProcessTargetOptions(processId)
                    {
                        NonInvasive = nonInvasive,
                        NoSuspend = noSuspend,
                        UseDbgEngSymOpts = useDbgEngSymOpts,
                        DbgEngEngineId = dbgEngEngineId
                    },
                    cancellationToken
                );
            }

            public DbgEngEngine OpenDump(
                string dumpFile,
#if DEBUG
                bool hookTTD = false,
#endif
                CancellationToken cancellationToken = default)
            {
                return (DbgEngEngine) debugEngineProvider.OpenDump(
                    DbgEngineKind.DbgEng,
                    new OpenDumpTargetOptions(dumpFile)
                    {
#if DEBUG
                        HookTTD = hookTTD
#endif
                    },
                    cancellationToken
                );
            }

            public DbgEngEngine ConnectServer(
                DbgEngServerConnectionInfo connectionInfo,
                CancellationToken cancellationToken = default)
            {
                return (DbgEngEngine) debugEngineProvider.ConnectServer(
                    DbgEngineKind.DbgEng,
                    new ServerTargetOptions(connectionInfo),
                    cancellationToken
                );
            }
        }
    }
}
