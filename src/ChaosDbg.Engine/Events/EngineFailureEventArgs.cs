using System;

namespace ChaosDbg
{
    public enum EngineFailureStatus
    {
        None,

        /// <summary>
        /// Specifies that the engine is attempting to gracefully shutdown.
        /// </summary>
        BeginShutdown,

        /// <summary>
        /// Specifies that the engine successfully shutdown.
        /// </summary>
        ShutdownSuccess,

        /// <summary>
        /// Specifies that a massive failure occurred and that the engine was unable to shutdown.
        /// The debugger process should be restarted.
        /// </summary>
        ShutdownFailure
    }

    public class EngineFailureEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public EngineFailureStatus Status { get; }

        public EngineFailureEventArgs(Exception exception, EngineFailureStatus status)
        {
            Exception = exception;
            Status = status;
        }
    }
}
