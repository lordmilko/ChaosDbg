using System;
using System.Threading;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public interface ICordbEngine : IDbgEngine, IDisposable
    {
        #region Control

        void CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default);

        void Attach(AttachProcessOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes the current process.<para/>
        /// This method will repeatedly call <see cref="CorDebugController.Continue(bool)"/> until
        /// <see cref="CordbSessionInfo.CurrentStopCount"/> is 0, indicating that the process is now
        /// freely running
        /// </summary>
        void Continue();

        void Break();

        #endregion
    }
}
