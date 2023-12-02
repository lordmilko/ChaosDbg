using System;
using System.Threading;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a command that should be dispatched to and executed on the engine thread.
    /// </summary>
    class DebugEngineCommand
    {
        public Either<Action, Func<object>> Action { get; set; }

        public SemaphoreSlim Semaphore { get; }

        public object Result { get; set; }

        public DebugEngineCommand(Func<object> func)
        {
            Action = func;
            Semaphore = new SemaphoreSlim(0, 1);
        }

        public DebugEngineCommand(Action func)
        {
            Action = func;
            Semaphore = new SemaphoreSlim(0, 1);
        }
    }
}
