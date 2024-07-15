using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.Metadata;

namespace ChaosDbg
{
    public partial class DebugEngineProvider
    {
        public class CordbEngineLaunchExtensions
        {
            private readonly DebugEngineProvider debugEngineProvider;

            public CordbEngineLaunchExtensions(DebugEngineProvider debugEngineProvider)
            {
                this.debugEngineProvider = debugEngineProvider;
            }

            public CordbEngine CreateProcess(
                string commandLine,
                bool startMinimized = false,
                bool useInterop = false,
                FrameworkKind? frameworkKind = null,
                CancellationToken cancellationToken = default)
            {
                return (CordbEngine) debugEngineProvider.CreateProcess(
                    useInterop ? DbgEngineKind.Interop : DbgEngineKind.Cordb,
                    new CreateProcessTargetOptions(commandLine)
                    {
                        StartMinimized = startMinimized,
                        UseInterop = useInterop,
                        FrameworkKind = frameworkKind
                    },
                    cancellationToken
                );
            }

            public CordbEngine Attach(
                int processId,
                bool useInterop = false,
                CancellationToken cancellationToken = default)
            {
                return (CordbEngine) debugEngineProvider.Attach(
                    useInterop ? DbgEngineKind.Interop : DbgEngineKind.Cordb,
                    new AttachProcessTargetOptions(processId)
                    {
                        UseInterop = useInterop
                    },
                    cancellationToken
                );
            }
        }
    }
}
