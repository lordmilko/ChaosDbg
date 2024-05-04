using System;
using System.Collections.Generic;
using ClrDebug;
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
        public T Invoke<T>(Func<DebugClient, T> func) =>
            Session.EngineThread.Invoke(() => func(Session.EngineClient));

        public void Invoke(Action<DebugClient> action) =>
            Session.EngineThread.Invoke(() => action(Session.EngineClient));

        public void Execute(string command) =>
            Invoke(c => c.Control.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT));

        public string[] ExecuteBufferedCommand(string command) =>
            ExecuteBufferedCommand(c => c.Control.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT));

        /// <summary>
        /// Executes a command that emits string values to output callbacks that should be captured and returned
        /// without affecting the output of the primary output callbacks.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>The text that was emitted by the command to the output callbacks.</returns>
        public string[] ExecuteBufferedCommand(Action<DebugClient> action) =>
            Session.EngineThread.Invoke(() => Session.ExecuteBufferedCommand(action));

        public void WaitForBreak() => Session.BreakEvent.Task.Wait();

        public DbgEngFrame[] GetStackTrace()
        {
            return Invoke(engine =>
            {
                //g_DefaultStackTraceDepth is 256 (0x100) in modern versions of DbgEng
                var frames = engine.Control.GetStackTrace(0, 0, 0, 256);

                var results = new List<DbgEngFrame>();

                foreach (var frame in frames)
                {
                    string name = null;

                    if (engine.Symbols.TryGetNameByOffset(frame.InstructionOffset, out var symbol) == HRESULT.S_OK)
                    {
                        name = symbol.NameBuffer;

                        if (symbol.Displacement > 0)
                            name = $"{name}+{symbol.Displacement:X}";
                    }

                    results.Add(new DbgEngFrame(name, frame));
                }

                return results.ToArray();
            });
        }
    }
}
