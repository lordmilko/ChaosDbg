using System.Threading;

namespace ChaosDbg
{
    internal interface IDbgEngineInternal : IDbgEngine
    {
        void CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default);

        void Attach(AttachProcessOptions options, CancellationToken cancellationToken = default);
    }
}
