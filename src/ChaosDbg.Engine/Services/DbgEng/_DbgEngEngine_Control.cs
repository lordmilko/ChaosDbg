using System;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Debugger interaction (start, stop, break, step, etc)

    partial class DbgEngEngine
    {
        /// <summary>
        /// Executes a command in the context of the engine thread.
        /// </summary>
        /// <typeparam name="T">The type of value that is returned from the command.</typeparam>
        /// <param name="func">The command to execute.</param>
        /// <returns>The result of the executed command.</returns>
        public T ExecuteCommand<T>(Func<DebugClient, T> func) =>
            Commands.ExecuteInEngine(func);

        /// <summary>
        /// Executes a command that emits string values to output callbacks that should be captured and returned
        /// without affecting the output of the primary output callbacks.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>The text that was emitted by the command to the output callbacks.</returns>
        public string[] ExecuteBufferedCommand(Action<DebugClient> action) =>
            Commands.ExecuteInEngine(_ => Session.ExecuteBufferedCommand(action));
    }
}
