using System;
using System.Threading;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents a command that should be dispatched to and executed on the engine thread.
    /// </summary>
    class DebugEngineCommand
    {
        public Either<Action<DebugClient>, Func<DebugClient, object>> Action { get; set; }

        public SemaphoreSlim Semaphore { get; }

        public object Result { get; set; }

        public DebugEngineCommand(Func<DebugClient, object> func)
        {
            Action = func;
            Semaphore = new SemaphoreSlim(0, 1);
        }

        public DebugEngineCommand(Action<DebugClient> action)
        {
            Action = action;
            Semaphore = new SemaphoreSlim(0, 1);
        }
    }
}
