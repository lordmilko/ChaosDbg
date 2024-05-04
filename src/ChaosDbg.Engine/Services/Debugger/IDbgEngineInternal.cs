using System.ComponentModel;
using System.Threading;

namespace ChaosDbg
{
    internal interface IDbgEngineInternal : IDbgEngine
    {
        void CreateProcess(LaunchTargetOptions options, CancellationToken cancellationToken = default);

        void Attach(LaunchTargetOptions options, CancellationToken cancellationToken = default);

        EventHandlerList EventHandlers { get; }
    }
}
