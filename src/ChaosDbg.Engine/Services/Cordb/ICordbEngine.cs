using System;
using System.Threading;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public interface ICordbEngine : IDbgEngine, IDisposable
    {
        #region Control

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
