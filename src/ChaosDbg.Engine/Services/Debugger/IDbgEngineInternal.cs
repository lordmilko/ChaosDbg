using System.ComponentModel;
using System.Threading;

namespace ChaosDbg
{
    internal interface IDbgEngineInternal : IDbgEngine
    {
        void CreateProcess(CreateProcessTargetOptions options, CancellationToken cancellationToken = default);

        void Attach(AttachProcessTargetOptions options, CancellationToken cancellationToken = default);
        
        void OpenDump(OpenDumpTargetOptions options, CancellationToken cancellationToken = default);

        EventHandlerList EventHandlers { get; }
    }
}
