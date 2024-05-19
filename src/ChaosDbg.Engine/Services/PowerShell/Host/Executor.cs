using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace ChaosDbg.PowerShell.Host
{
    //Based on https://github.com/PowerShell/PowerShell/blob/da1ca4a7266e2c9e43f96a38aeea8092ace71cca/src/Microsoft.PowerShell.ConsoleHost/host/msh/Executor.cs

    class Executor
    {
        [Flags]
        internal enum ExecutionOptions
        {
            None,
            AddOutDefault,
            AddToHistory
        }

        private const string OutDefaultCommandName = "Out-Default";
        private PSHostBase host;
        private bool isPromptFunctionExecutor;
        private object instanceStateLock = new object();
        private static object staticStateLock = new object();
        private Pipeline pipeline;
        private static Executor s_currentExecutor;
        private bool cancelled;
        private static MethodInfo Command_IsEndOfStatement_Set = typeof(Command).GetProperty(nameof(Command.IsEndOfStatement)).GetSetMethod(true);

        internal static void CancelCurrentExecutor()
        {
            Executor temp = null;

            lock (staticStateLock)
            {
                temp = s_currentExecutor;
            }

            temp?.Cancel();
        }

        internal static Executor CurrentExecutor
        {
            get
            {
                Executor result = null;

                lock (staticStateLock)
                {
                    result = s_currentExecutor;
                }

                return result;
            }

            set
            {
                lock (staticStateLock)
                {
                    // null is acceptable.

                    s_currentExecutor = value;
                }
            }
        }

        internal Executor(PSHostBase host, bool isPromptFunctionExecutor)
        {
            this.host = host;
            this.isPromptFunctionExecutor = isPromptFunctionExecutor;
        }

        internal string ExecuteCommandAndGetResultAsString(string command, out Exception exception)
        {
            var results = ExecuteCommand(command, out exception, ExecutionOptions.None);

            if (exception != null || results == null || results.Count == 0)
                return null;

            if (results[0] == null)
                return string.Empty;

            var first = results[0];

            return first.BaseObject.ToString();
        }

        internal Collection<PSObject> ExecuteCommand(
            string command,
            out Exception exception,
            ExecutionOptions options)
        {
            var pipeline = host.Runspace.CreatePipeline(command, options.HasFlag(ExecutionOptions.AddToHistory));

            return ExecutePipeline(pipeline, out exception, options);
        }

        internal Collection<PSObject> ExecutePipeline(Pipeline pipeline, out Exception exception, ExecutionOptions options)
        {
            exception = null;

            if (options.HasFlag(ExecutionOptions.AddOutDefault))
                AddOutDefault(pipeline);

            Collection<PSObject> results = null;

            Executor oldCurrent = CurrentExecutor;
            CurrentExecutor = this;

            lock (instanceStateLock)
            {
                Debug.Assert(this.pipeline == null, "no other pipeline should exist");
                this.pipeline = pipeline;
            }

            try
            {
                results = pipeline.Invoke();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                // Once we have the results, or an exception is thrown, we throw away the pipeline.

                ((ChaosHostUserInterface) host.UI).ResetProgress();
                CurrentExecutor = oldCurrent;
                Reset();
            }

            return results;
        }

        private void AddOutDefault(Pipeline pipeline)
        {
            if (pipeline.Commands.Count < 2)
            {
                if (pipeline.Commands.Count == 1)
                    pipeline.Commands[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);

                pipeline.Commands.Add(GetOutDefaultCommand(endOfStatement: false));
            }
            else
            {
                //Add Out-Default to the end of each statement

                var newCommands = new List<Command>();

                foreach (var cmd in pipeline.Commands)
                {
                    newCommands.Add(cmd);

                    if (cmd.IsEndOfStatement)
                    {
                        Command_IsEndOfStatement_Set.Invoke(cmd, new object[] {false});
                        newCommands.Add(GetOutDefaultCommand(endOfStatement: true));
                    }
                }

                var last = newCommands.Last();

                if (!IsOutDefault(last))
                    newCommands.Add(GetOutDefaultCommand(endOfStatement: false));

                //Replace all the commands with our new list that uses Out-Default everywhere

                pipeline.Commands.Clear();

                foreach (var cmd in newCommands)
                    pipeline.Commands.Add(cmd);
            }
        }

        private Command GetOutDefaultCommand(bool endOfStatement)
        {
            var command = new Command(
                command: OutDefaultCommandName,
                isScript: false,
                useLocalScope: true
            )
            {
                MergeUnclaimedPreviousCommandResults = PipelineResultTypes.Error | PipelineResultTypes.Output
            };

            if (endOfStatement)
                Command_IsEndOfStatement_Set.Invoke(command, new object[] { true });

            return command;
        }

        /// <summary>
        /// Executes a command (by calling this.ExecuteCommand), and coerces the first result object to a bool. Any Exception
        /// thrown in the course of execution is caught and ignored.
        /// </summary>
        /// <param name="command">
        /// The command to execute. May be any valid monad command.
        /// </param>
        /// <returns>
        /// The Nullable`bool representation of the first result object returned, or null if an exception was thrown or no
        /// objects were returned by the command.
        /// </returns>
        internal bool? ExecuteCommandAndGetResultAsBool(string command)
        {
            bool? result = ExecuteCommandAndGetResultAsBool(command, out _);

            return result;
        }

        /// <summary>
        /// Executes a command (by calling this.ExecuteCommand), and coerces the first result object to a bool. Any Exception
        /// thrown in the course of execution is returned through the exceptionThrown parameter.
        /// </summary>
        /// <param name="command">
        /// The command to execute. May be any valid monad command.
        /// </param>
        /// <param name="exceptionThrown">
        /// Receives the Exception thrown by the execution of the command, if any. Set to null if no exception is thrown.
        /// Can be tested to see if the execution was successful or not.
        /// </param>
        /// <returns>
        /// The Nullable`bool representation of the first result object returned, or null if an exception was thrown or no
        /// objects were returned by the command.
        /// </returns>
        internal bool? ExecuteCommandAndGetResultAsBool(string command, out Exception exceptionThrown)
        {
            exceptionThrown = null;

            Debug.Assert(!string.IsNullOrEmpty(command), "command should have a value");

            bool? result = null;

            do
            {
                Collection<PSObject> streamResults = ExecuteCommand(command, out exceptionThrown, ExecutionOptions.None);

                if (exceptionThrown != null)
                {
                    break;
                }

                if (streamResults == null || streamResults.Count == 0)
                {
                    break;
                }

                // we got back one or more objects.

                result = (streamResults.Count > 1) || (LanguagePrimitives.IsTrue(streamResults[0]));
            }
            while (false);

            return result;
        }

        private void Reset()
        {
            lock (instanceStateLock)
            {
                pipeline = null;
                cancelled = false;
            }
        }

        private void Cancel()
        {
            // if there's a pipeline running, stop it.

            lock (instanceStateLock)
            {
                if (pipeline != null && !cancelled)
                {
                    cancelled = true;

                    if (isPromptFunctionExecutor)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    pipeline.Stop();
                }
            }
        }

        private bool IsOutDefault(Command command) =>
            command.CommandText != null && command.CommandText.Equals(OutDefaultCommandName, StringComparison.OrdinalIgnoreCase);
    }
}
