using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ChaosDbg.Cordb
{
    class CordbCommandStore
    {
        private BlockingCollection<DebugEngineCommand> queue = new BlockingCollection<DebugEngineCommand>();

        private CordbEngine engine;

        public CordbCommandStore(CordbEngine engine)
        {
            this.engine = engine;
        }

        #region Local

        public void ExecuteInEngine(Action action)
        {
            if (engine.Session.EngineThreadId == Thread.CurrentThread.ManagedThreadId)
                action();
            else
            {
                var command = new DebugEngineCommand(action);

                queue.Add(command);

                command.Semaphore.Wait();
            }
        }

        /// <summary>
        /// Executes a specified function within the context of the engine thread.
        /// </summary>
        /// <typeparam name="T">The type of value that is returned from the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        public T ExecuteInEngine<T>(Func<T> func)
        {
            if (engine.Session.EngineThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                //We're already on the engine thread, just execute the callback
                return func();
            }
            else
            {
                var command = new DebugEngineCommand(() => func());

                queue.Add(command);

                //engine.WakeEngineForInput();

                command.Semaphore.Wait();

                return (T) command.Result;
            }
        }

        #endregion
        #region Remote

        /// <summary>
        /// Processes all commands in the queue until the queue is empty.
        /// </summary>
        public void DrainQueue()
        {
            while (queue.Count > 0)
            {
                var command = queue.Take(engine.Session.EngineCancellationToken);

                if (command.Action.IsLeft)
                    command.Action.Left();
                else
                    command.Result = command.Action.Right();

                command.Semaphore.Release();
            }
        }

        #endregion
    }
}
