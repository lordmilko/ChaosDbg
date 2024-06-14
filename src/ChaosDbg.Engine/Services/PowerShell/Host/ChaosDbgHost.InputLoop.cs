using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using ChaosLib;

namespace ChaosDbg.PowerShell.Host
{
    //Based on https://github.com/PowerShell/PowerShell/blob/da1ca4a7266e2c9e43f96a38aeea8092ace71cca/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHost.cs

    internal partial class PSHostBase
    {
        class InputLoop
        {
            private PSHostBase host;
            private Executor exec;
            private Executor promptExec;
            private bool isNested;
            private bool shouldExit;
            private bool isRunspacePushed;
            private bool runspacePopped;
            private object objLock = new object();
            private ConstructorInfo PSCommand_Ctor = typeof(PSCommand).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] {typeof(Command)}, null);

            internal static void RunNewInputLoop(PSHostBase host, bool isNested)
            {
                var loop = new InputLoop(host, isNested);

                host.InputStack.Push(loop);

                loop.Run();
            }

            internal static bool ExitCurrentLoop(PSHostBase host)
            {
                if (host.InputStack.Count == 0)
                    throw new PSInvalidOperationException("Cannot process input loop. ExitCurrentLoop was called when no InputLoops were running.");

                InputLoop il = host.InputStack.Peek();
                il.shouldExit = true;

                // The main (non-nested) input loop has Count == 1,
                // so Count == 2 is the value that indicates the next
                // popped stack input loop is non-nested.
                return (host.InputStack.Count > 2);
            }

            /// <summary>
            /// Returns current root (non-nested) loop only if there is no
            /// nesting.  This is used *only* by the debugger for remote debugging
            /// where data handling on the base commands needs to be blocked
            /// during remote debug stop handling.
            /// </summary>
            /// <returns></returns>
            internal static InputLoop GetNonNestedLoop(PSHostBase host)
            {
                if (host.InputStack.Count == 1)
                {
                    return host.InputStack.Peek();
                }

                return null;
            }

            private InputLoop(PSHostBase host, bool isNested)
            {
                this.host = host;
                this.isNested = isNested;
                host.RunspacePopped += HandleRunspacePopped;
                host.RunspacePushed += HandleRunspacePushed;
                exec = new Executor(host, isPromptFunctionExecutor: false);
                promptExec = new Executor(host, isPromptFunctionExecutor: true);
            }

            private void Run()
            {
                ChaosHostUserInterface ui = (ChaosHostUserInterface) host.UI;

                bool inBlockMode = false; //when the prompt is like >> because you did an incomplete command
                var inputBlock = new StringBuilder();
                // Use nullable so that we don't evaluate suggestions at startup.
                bool? previousResponseWasEmpty = null;

                while (!host.ShouldEndSession && !shouldExit)
                {
                    try
                    {
                        string line;

                        try
                        {
                            host.terminal.LockProtection(() =>
                            {
                                WritePrompt(ui, inBlockMode);

                                host.terminal.EnterWriteProtection();
                            });

                            line = ui.ReadLineWithTabCompletion();
                        }
                        finally
                        {
                            host.terminal.ExitWriteProtection();
                        }

                        if (host.TryExecuteDbgCommand(line))
                            continue;

                        if (line == null)
                        {
                            //User pressed Ctrl+C

                            previousResponseWasEmpty = true;

                            if (!ui.ReadFromStdin)
                            {
                                // If we're not reading from stdin, the we probably got here
                                // because the user hit ctrl-C. Do a writeline to clean up
                                // the output...
                                ui.WriteLine();
                            }

                            inBlockMode = false;

                            if (Console.IsInputRedirected)
                            {
                                // null is also the result of reading stdin to EOF.
                                host.ShouldEndSession = true;
                                break;
                            }

                            continue;
                        }
                        else if (string.IsNullOrWhiteSpace(line))
                        {
                            if (inBlockMode)
                            {
                                // end block mode and execute the block accumulated block

                                line = inputBlock.ToString();
                                inBlockMode = false;
                            }
                            else if (!host.InDebugMode)
                            {
                                previousResponseWasEmpty = true;
                                continue;
                            }
                        }
                        else
                        {
                            if (inBlockMode)
                            {
                                inputBlock.Append('\n');
                                inputBlock.Append(line);
                                continue;
                            }
                        }

                        Debug.Assert(line != null, "line should not be null");
                        Debug.Assert(line.Length > 0 || host.InDebugMode, "line should not be empty unless the host is in debug mode");
                        Debug.Assert(!inBlockMode, "should not be in block mode at point of pipeline execution");

                        if (host.InDebugMode)
                        {
                            ProcessDebugMode(line, ref inBlockMode, ref inputBlock);

                            continue;
                        }

                        if (runspacePopped)
                        {
                            var msg = $"Command '{line}' was not run as the session in which it was intended to run was either closed or broken";
                            ui.WriteErrorLine(msg);
                            runspacePopped = false;
                        }
                        else
                        {
                            //If we disabled ENABLE_PROCESSED_INPUT to allow F11 stepping, we need to re-enable it for the duration of the pipeline
                            //so we can Ctrl+C cancel it

                            var oldMode = host.terminal.GetInputConsoleMode();

                            var newMode = oldMode | ConsoleMode.ENABLE_PROCESSED_INPUT;

                            if (oldMode != newMode)
                                host.terminal.SetInputConsoleMode(newMode);

                            Exception e;

                            try
                            {
                                exec.ExecuteCommand(line, out e, Executor.ExecutionOptions.AddOutDefault | Executor.ExecutionOptions.AddToHistory);
                            }
                            finally
                            {
                                if (oldMode != newMode)
                                    host.terminal.SetInputConsoleMode(oldMode);
                            }

                            Thread bht = null;

                            lock (host.hostGlobalLock)
                            {
                                bht = host.breakHandlerThread;
                            }

                            bht?.Join();

                            // Once the pipeline has been executed, we toss any outstanding progress data and
                            // take down the display.

                            ui.ResetProgress();

                            if (e != null)
                            {
                                // Handle incomplete parse and other errors.
                                inBlockMode = HandleErrors(e, line, inBlockMode, ref inputBlock);

                                // If a remote runspace is pushed and it is not in a good state
                                // then pop it.
                                if (isRunspacePushed && (host.Runspace != null) &&
                                    ((host.Runspace.RunspaceStateInfo.State != RunspaceState.Opened) ||
                                     (host.Runspace.RunspaceAvailability != RunspaceAvailability.Available)))
                                {
                                    host.PopRunspace();
                                }
                            }
                        }
                    }
                    finally
                    {
                        host.isRunningPromptLoop = false;
                    }
                }
            }

            private void HandleRunspacePushed(object sender, EventArgs e)
            {
                lock (objLock)
                {
                    isRunspacePushed = true;
                    runspacePopped = false;
                }
            }


            /// <summary>
            /// When a runspace is popped, we need to reevaluate the
            /// prompt.
            /// </summary>
            /// <param name="sender">Sender of this event, unused.</param>
            /// <param name="eventArgs">Arguments describing this event, unused.</param>
            private void HandleRunspacePopped(object sender, EventArgs eventArgs)
            {
                lock (objLock)
                {
                    isRunspacePushed = false;
                    runspacePopped = true;
                }
            }

            #region Debug

            private void ProcessDebugMode(string line, ref bool inBlockMode, ref StringBuilder inputBlock)
            {
                DebuggerCommandResults results = ProcessDebugCommand(line, out var e);

                if (results.ResumeAction != null)
                {
                    host.ExitDebugMode(results.ResumeAction.Value);
                }

                if (e != null)
                {
                    var ex = e as PSInvalidOperationException;
                    if (e is PSRemotingTransportException ||
                        e is RemoteException ||
                        (ex != null &&
                         ex.ErrorRecord != null &&
                         ex.ErrorRecord.FullyQualifiedErrorId.Equals("Debugger:CannotProcessCommandNotStopped", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Debugger session is broken.  Exit nested loop.
                        host.ExitDebugMode(DebuggerResumeAction.Continue);
                    }
                    else
                    {
                        // Handle incomplete parse and other errors.
                        inBlockMode = HandleErrors(e, line, inBlockMode, ref inputBlock);
                    }
                }
            }

            private DebuggerCommandResults ProcessDebugCommand(string cmd, out Exception e)
            {
                DebuggerCommandResults results = null;

                try
                {
                    host.DebuggerCanStopCommand = true;

                    // Use PowerShell object to write streaming data to host.
                    using (System.Management.Automation.PowerShell ps = System.Management.Automation.PowerShell.Create())
                    {
                        var settings = new PSInvocationSettings()
                        {
                            Host = host
                        };

                        var output = new PSDataCollection<PSObject>();
                        ps.AddCommand("Out-Default");
                        var async = ps.BeginInvoke(output, settings, null, null);

                        // Let debugger evaluate command and stream output data.
                        results = host.Runspace.Debugger.ProcessCommand(
                            (PSCommand) PSCommand_Ctor.Invoke(null, new object[] { cmd, true }),
                            output);

                        output.Complete();
                        ps.EndInvoke(async);
                    }

                    e = null;
                }
                catch (Exception ex)
                {
                    e = ex;
                    results = new DebuggerCommandResults(null, false);
                }
                finally
                {
                    host.DebuggerCanStopCommand = false;
                }

                // Exit debugger if command fails to evaluate.
                return results ?? new DebuggerCommandResults(DebuggerResumeAction.Continue, false);
            }

            #endregion
            #region Errors

            private bool HandleErrors(Exception e, string line, bool inBlockMode, ref StringBuilder inputBlock)
            {
                Debug.Assert(e != null, "Exception reference should not be null.");

                if (IsIncompleteParseException(e))
                {
                    if (!inBlockMode)
                    {
                        inBlockMode = true;
                        inputBlock = new StringBuilder(line);
                    }
                    else
                    {
                        inputBlock.Append(line);
                    }
                }
                else
                {
                    // an exception occurred when the command was executed.  Tell the user about it.
                    host.ReportException(e, exec);
                }

                return inBlockMode;
            }

            private static bool IsIncompleteParseException(Exception e)
            {
                // Check e's type.
                if (e is IncompleteParseException)
                {
                    return true;
                }

                // If it is remote exception ferret out the real exception.
                if (e is not RemoteException remoteException || remoteException.ErrorRecord == null)
                {
                    return false;
                }

                return remoteException.ErrorRecord.CategoryInfo.Reason == nameof(IncompleteParseException);
            }

            #endregion
            #region Prompt

            private void WritePrompt(PSHostUserInterface ui, bool inBlockMode)
            {
                string prompt;

                if (inBlockMode)
                    prompt = ">> ";
                else
                {
                    if (ui.RawUI.CursorPosition.X != 0)
                        ui.WriteLine();

                    if (host.InDebugMode)
                        throw new NotImplementedException();

                    prompt = EvaluatePrompt();
                }

                ui.Write(prompt);
            }

            private string EvaluatePrompt()
            {
                var promptString = promptExec.ExecuteCommandAndGetResultAsString("prompt", out _);

                if (string.IsNullOrEmpty(promptString))
                    promptString = "PS>";

                return promptString;
            }

            #endregion
        }
    }
}
