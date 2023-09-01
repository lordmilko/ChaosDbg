using System;
using System.Collections.Concurrent;
using System.Threading;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents a container around commands that should be dispatched to the engine thread, and manages queuing and dispatching commands
    /// as required.
    /// </summary>
    class DbgEngCommandStore
    {
        private BlockingCollection<DebugEngineCommand> queue = new BlockingCollection<DebugEngineCommand>();

        private DbgEngEngine engine;

        public DbgEngCommandStore(DbgEngEngine engine)
        {
            this.engine = engine;
        }

        #region Local

        /// <summary>
        /// Executes a specified function within the context of the engine thread.
        /// </summary>
        /// <typeparam name="T">The type of value that is returned from the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        public T ExecuteInEngine<T>(Func<DebugClient, T> func)
        {
            if (engine.Session.EngineThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                //We're already on the engine thread, just execute the callback
                return func(engine.Session.EngineClient);
            }
            else
            {
                var command = new DebugEngineCommand(c => func(c));

                queue.Add(command);

                engine.WakeEngineForInput();

                command.Semaphore.Wait();

                return (T)command.Result;
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

                var engineClient = engine.Session.EngineClient;

                if (command.Action.IsLeft)
                    command.Action.Left(engineClient);
                else
                    command.Result = command.Action.Right(engineClient);

                command.Semaphore.Release();
            }
        }

        #endregion
    }
}
