using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using ChaosDbg.Terminal;
using ChaosLib;
using SMA = System.Management.Automation;

namespace ChaosDbg.PowerShell.Host
{
    //Based on https://github.com/PowerShell/PowerShell/blob/da1ca4a7266e2c9e43f96a38aeea8092ace71cca/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHost.cs

    internal abstract partial class PSHostBase : PSHost
    {
        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name { get; } = "ChaosDbgHost";
        public override PSHostUserInterface UI { get; }
        public override Version Version { get; } = new Version(FileVersionInfo.GetVersionInfo(typeof(PSHostBase).Assembly.Location).ProductVersion);

        protected ITerminal terminal;
        private CommandLineParameterParser cpp;
        private object hostGlobalLock = new object();
        private Thread breakHandlerThread;
        private bool shouldEndSession;
        internal Pipeline runningCmd;
        internal bool isRunningPromptLoop;
        private Exception lastRunspaceInitializationException;
        private Stack<InputLoop> InputStack { get; } = new Stack<InputLoop>();
        
        protected PSHostBase(ITerminal terminal, CommandLineParameterParser cpp)
        {
            this.terminal = terminal;
            this.cpp = cpp;

            UI = new ConsoleChaosHostUserInterface(this, terminal);
        }

        public int Run()
        {
            terminal.AddBreakHandler(MyBreakHandler);

            return DoRunspaceLoop();
        }

        protected virtual bool TryExecuteDbgCommand(string command) => false;

        #region Runspace

        internal Runspace Runspace { get; private set; }

        /// <summary>
        /// True if a runspace is pushed; false otherwise.
        /// </summary>
        public bool IsRunspacePushed { get; private set; }

        /// <summary>
        /// Raised when the host pops a runspace.
        /// </summary>
        internal event EventHandler RunspacePopped;

        /// <summary>
        /// Raised when the host pushes a runspace.
        /// </summary>
        internal event EventHandler RunspacePushed;

        /// <summary>
        /// Loops over the Host's sole Runspace; opens the runspace, initializes it, then recycles it if the Runspace fails.
        /// </summary>
        /// <returns>
        /// The process exit code to be returned by Main.
        /// </returns>
        private int DoRunspaceLoop()
        {
            ExitCode = ExitCodeSuccess;

            while (!ShouldEndSession)
            {
                CreateRunspace();

                if (ExitCode == ExitCodeInitFailure)
                {
                    break;
                }

                if (!noExit)
                {
                    // Wait for runspace to open, init, and run init script before
                    // setting ShouldEndSession, to allow debugger to work.
                    ShouldEndSession = true;
                }
                else
                {
                    // Start nested prompt loop.
                    EnterNestedPrompt();
                }

                if (setShouldExitCalled)
                {
                    ExitCode = exitCodeFromRunspace;
                }
                else
                {
                    Executor exec = new Executor(this, false);

                    bool dollarHook = exec.ExecuteCommandAndGetResultAsBool("$global:?") ?? false;

                    if (dollarHook && (lastRunspaceInitializationException == null))
                    {
                        ExitCode = ExitCodeSuccess;
                    }
                    else
                    {
                        ExitCode = ExitCodeSuccess | 0x1;
                    }
                }

                Runspace.Close();
                Runspace = null;
            }

            return ExitCode;
        }

        private void CreateRunspace()
        {
            DoCreateRunspace();
        }

        private void DoCreateRunspace()
        {
            //The CreateDefault method creates an InitialSessionState with all of the built-in commands loaded, while the CreateDefault2
            //method loads only the commands required to host PowerShell (the commands from the Microsoft.PowerShell.Core module).
            var iss = InitialSessionState.CreateDefault2();
            iss.LanguageMode = PSLanguageMode.FullLanguage;

            if (!cpp.NonInteractive)
                iss.ImportPSModule(new[] { "PSReadLine" });

            iss.ImportPSModule(new[] {GetType().Assembly.Location});

            Runspace = RunspaceFactory.CreateRunspace(this, iss);

            //This is required to invoke PSReadLine directly without PowerShell. The default runspace is stored per-thread
            Runspace.DefaultRunspace = Runspace;

            //This is slow because it has to import PSReadLine and initialize the ps1xml type system
            Runspace.Open();

            //Running a user's profile is also slow
            DoRunspaceInitialization();
        }

        protected virtual void DoRunspaceInitialization()
        {
            if (Runspace.Debugger != null)
            {
                Runspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
                Runspace.Debugger.DebuggerStop += Debugger_DebuggerStop;
            }

            var exec = new Executor(this, isPromptFunctionExecutor: false);

            if (!cpp.SkipProfiles)
            {
                const string shellId = "Microsoft.PowerShell";

                var getProfileObjectData = typeof(HostUtilities).GetMethodInfo("GetProfileObjectData");

                var args = new object[] {
                    shellId, //shellId
                    false,   //useTestProfile
                    null,    //allUsersAllHosts
                    null,    //allUsersCurrentHost
                    null,    //currentUserAllHosts
                    null,    //currentUserCurrentHost
                    null     //dollarProfile
                };

                getProfileObjectData.Invoke(null, args);

                var allUsersAllHosts = (string) args[2];
                var allUsersCurrentHost = (string) args[3];
                var currentUserAllHosts = (string) args[4];
                var currentUserCurrentHost = (string) args[5];
                var dollarProfile = args[6];

                Runspace.SessionStateProxy.SetVariable("PROFILE", dollarProfile);

                RunProfile(allUsersAllHosts, exec);
                RunProfile(allUsersCurrentHost, exec);
                RunProfile(currentUserAllHosts, exec);
                RunProfile(currentUserCurrentHost, exec);
            }

            if (System.Diagnostics.Debugger.IsAttached)
                terminal.Clear();

            if (!string.IsNullOrEmpty(cpp.InitialCommand))
            {
                var pipeline = Runspace.CreatePipeline(cpp.InitialCommand, true);

                exec.ExecutePipeline(pipeline, out var ex, Executor.ExecutionOptions.AddOutDefault);

                if (ex != null)
                {
                    lastRunspaceInitializationException = ex;
                    ReportException(ex, exec);
                }
            }
        }

        private void RunProfile(string profileFileName, Executor exec)
        {
            if (!string.IsNullOrEmpty(profileFileName))
            {
                if (File.Exists(profileFileName))
                {
                    InitializeRunspaceHelper(
                        $". '{EscapeSingleQuotes(profileFileName)}'",
                        exec,
                        Executor.ExecutionOptions.AddOutDefault
                }
            }
        }

        private void InitializeRunspaceHelper(string command, Executor exec, Executor.ExecutionOptions options)
        {
            exec.ExecuteCommand(command, out var exception, options);

            if (exception != null)
                throw new NotImplementedException();
        }

        /// <summary>
        /// See base class.
        /// </summary>
        public void PopRunspace()
        {
            /*if (_runspaceRef == null || !_runspaceRef.IsRunspaceOverridden)
            {
                return;
            }

            if (_inPushedConfiguredSession)
            {
                // For configured endpoint sessions, end session when configured runspace is popped.
                this.ShouldEndSession = true;
            }

            if (Runspace.Debugger != null)
            {
                // Unsubscribe pushed runspace debugger.
                Runspace.Debugger.DebuggerStop -= OnExecutionSuspended;

                StopPipeline(this.runningCmd);

                if (this.InDebugMode)
                {
                    ExitDebugMode(DebuggerResumeAction.Continue);
                }
            }

            this.runningCmd = null;

            lock (hostGlobalLock)
            {
                _runspaceRef.Revert();
                _isRunspacePushed = false;
            }

            // Re-subscribe local runspace debugger.
            Runspace.Debugger.DebuggerStop += OnExecutionSuspended;

            // raise events outside the lock
            RunspacePopped?.Invoke(this, EventArgs.Empty);*/

            throw new NotImplementedException();
        }

        private static bool StopPipeline(Pipeline cmd)
        {
            if (cmd != null && (cmd.PipelineStateInfo.State is PipelineState.Running or PipelineState.Disconnected))
            {
                try
                {
                    cmd.StopAsync();
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        private static string EscapeSingleQuotes(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length * 2);

            foreach (var c in str)
            {
                if (c == '\'')
                    sb.Append(c);

                sb.Append(c);
            }

            var result = sb.ToString();

            return result;
        }

        #endregion
        #region Nested

        internal bool IsNested { get; set; }

        /// <summary>
        /// Instructs the host to interrupt the currently running pipeline and start a new, "nested" input loop,
        /// where an input loop is the cycle of prompt, input, execute.
        /// </summary>
        /// <remarks>
        /// Typically called by the engine in response to some user action that suspends the currently executing pipeline,
        /// such as choosing the "suspend" option of a ConfirmProcessing call. Before calling this method, the engine should set
        /// various shell variables to the express the state of the interrupted input loop (current pipeline, current object in pipeline,
        /// depth of nested input loops, etc.)
        ///
        /// A non-interactive host may throw a "not implemented" exception here.
        ///
        /// If the UI property returns null, the engine should not call this method.
        /// </remarks>
        public override void EnterNestedPrompt()
        {
            // save the old Executor, then clear it so that a break does not cancel the pipeline from which this method
            // might be called.

            Executor oldCurrent = Executor.CurrentExecutor;

            try
            {
                // this assignment is threadsafe -- protected in CurrentExecutor property

                Executor.CurrentExecutor = null;
                lock (hostGlobalLock)
                {
                    IsNested = oldCurrent != null || ((ChaosHostUserInterface) UI).IsCommandCompletionRunning;
                }

                InputLoop.RunNewInputLoop(this, IsNested);
            }
            finally
            {
                Executor.CurrentExecutor = oldCurrent;
            }
        }

        /// <summary>
        /// Instructs the host to interrupt the currently running pipeline and start a new, "nested" input loop, where an input loop
        /// is the cycle of prompt, input, execute.
        /// </summary>
        public override void ExitNestedPrompt()
        {
            lock (hostGlobalLock)
            {
                IsNested = InputLoop.ExitCurrentLoop(this);
        }
                }

        #endregion
        #region Debug

        private bool InDebugMode { get; set; }

        internal bool DebuggerCanStopCommand { get; set; }

        private DebuggerStopEventArgs debuggerStopEventArgs;
        private bool displayDebuggerBanner;

        /// <summary>
        /// Sets the host to debug mode and enters a nested prompt.
        /// </summary>
        private void EnterDebugMode()
        {
            InDebugMode = true;

            try
            {
                //
                // Note that we need to enter the nested prompt via the InternalHost interface.
                //

                // EnterNestedPrompt must always be run on the local runspace.
                //Runspace runspace = _runspaceRef.OldRunspace ?? this.RunspaceRef.Runspace;
                //runspace.ExecutionContext.EngineHostInterface.EnterNestedPrompt();
                throw new NotImplementedException();
            }
            catch (PSNotImplementedException)
            {
                WriteDebuggerMessage("The current session does not support debugging; execution will continue.");
            }
            finally
            {
                InDebugMode = false;
            }
        }

        /// <summary>
        /// Exits the debugger's nested prompt.
        /// </summary>
        private void ExitDebugMode(DebuggerResumeAction resumeAction)
        {
            debuggerStopEventArgs.ResumeAction = resumeAction;

            try
            {
                //
                // Note that we need to exit the nested prompt via the InternalHost interface.
                //

                // ExitNestedPrompt must always be run on the local runspace.
                //Runspace runspace = _runspaceRef.OldRunspace ?? this.RunspaceRef.Runspace;
                //runspace.ExecutionContext.EngineHostInterface.ExitNestedPrompt();
                throw new NotImplementedException();
            }
            catch (Exception ex) when (ex.GetType().Name == "ExitNestedPromptException")
            {
                // ignore the exception
        }
                }

        /// <summary>
        /// Handler for debugger events.
        /// </summary>
        private void OnExecutionSuspended(object sender, DebuggerStopEventArgs e)
        {
            // Check local runspace internalHost to see if debugging is enabled.
            /*LocalRunspace localrunspace = LocalRunspace;
            if ((localrunspace != null) && !localrunspace.ExecutionContext.EngineHostInterface.DebuggerEnabled)
            {
                return;
            }*/
            throw new NotImplementedException();

            debuggerStopEventArgs = e;
            InputLoop baseLoop = null;

            try
            {
                if (this.IsRunspacePushed)
                {
                    // For remote debugging block data coming from the main (not-nested)
                    // running command.
                    baseLoop = InputLoop.GetNonNestedLoop(this);
                    //baseLoop?.BlockCommandOutput();
                    throw new NotImplementedException();
            }

                //
                // Display the banner only once per session
                //
                if (displayDebuggerBanner)
                {
                    WriteDebuggerMessage("Entering debug mode. Use h or ? for help.");
                    WriteDebuggerMessage(string.Empty);
                    displayDebuggerBanner = false;
        }

                //
                // If we hit a breakpoint output its info
                //
                if (e.Breakpoints.Count > 0)
                {
                    string format = "Hit {0}";

                    foreach (Breakpoint breakpoint in e.Breakpoints)
                    {
                        WriteDebuggerMessage(string.Format(CultureInfo.CurrentCulture, format, breakpoint));
                    }

                    WriteDebuggerMessage(string.Empty);
                }

                //
                // Write the source line
                //
                if (e.InvocationInfo != null)
                {
                    //    line = StringUtil.Format(ConsoleHostStrings.DebuggerSourceCodeFormat, scriptFileName, e.InvocationInfo.ScriptLineNumber, e.InvocationInfo.Line);
                    WriteDebuggerMessage(e.InvocationInfo.PositionMessage);
                }

                //
                // Start the debug mode
                //
                EnterDebugMode();
            }
            finally
            {
                debuggerStopEventArgs = null;
                //baseLoop?.ResumeCommandOutput();
                throw new NotImplementedException();
            }
        }

        private void Debugger_DebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes a line using the debugger colors.
        /// </summary>
        private void WriteDebuggerMessage(string line)
        {
            var ui = (ChaosHostUserInterface) UI;

            UI.WriteLine(ui.DebugForegroundColor, ui.DebugBackgroundColor, line);
        }

        private bool BreakIntoDebugger()
        {
            SMA.Debugger debugger = null;
            lock (hostGlobalLock)
            {
                /*if (Runspace != null && Runspace.GetCurrentlyRunningPipeline() != null)
                {
                    debugger = Runspace.Debugger;
                }*/
                throw new NotImplementedException();
            }

            if (debugger != null)
            {
                debugger.SetDebuggerStepMode(true);
                return true;
            }

            return false;
        }

        #endregion
        #region Ctrl+C

        private bool MyBreakHandler(ConsoleControlType dwCtrlType)
        {
            switch (dwCtrlType)
            {
                case ConsoleControlType.CTRL_BREAK_EVENT:
                    if (cpp.NonInteractive)
                        SpinUpBreakHandlerThread(shouldEndSession: true);
                    else
                        BreakIntoDebugger();
                    return true;

                case ConsoleControlType.CTRL_C_EVENT:
                    SpinUpBreakHandlerThread(shouldEndSession: false);
                    return true;

                case ConsoleControlType.CTRL_LOGOFF_EVENT:
                    return true;

                case ConsoleControlType.CTRL_CLOSE_EVENT:
                case ConsoleControlType.CTRL_SHUTDOWN_EVENT:
                    SpinUpBreakHandlerThread(shouldEndSession: true);
                    return false;

                default:
                    SpinUpBreakHandlerThread(shouldEndSession: true);
                    return false;
            }
        }

        private void SpinUpBreakHandlerThread(bool shouldEndSession)
        {
            Thread bht = null;

            lock (hostGlobalLock)
            {
                bht = breakHandlerThread;
                if (!ShouldEndSession && shouldEndSession)
                {
                    ShouldEndSession = shouldEndSession;
                }

                // Creation of the thread and starting it should be an atomic operation.
                // otherwise the code in Run method can get instance of the breakhandlerThread
                // after it is created and before started and call join on it. This will result
                // in ThreadStateException.
                // NTRAID#Windows OutofBand Bugs-938289-2006/07/27-hiteshr
                if (bht == null)
                {
                    // we're not already running HandleBreak on a separate thread, so run it now.

                    breakHandlerThread = new Thread(HandleBreak)
                    {
                        Name = "ConsoleHost.HandleBreak"
                    };
                    breakHandlerThread.Start();
                }
            }
        }

        private void HandleBreak()
        {
            if (InDebugMode)
            {
                // Only stop a running user command, ignore prompt evaluation.
                if (DebuggerCanStopCommand)
                {
                    // Cancel any executing debugger command if in debug mode.
                    try
                    {
                        Runspace.Debugger.StopProcessCommand();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            else
            {
                // Cancel the reconnected debugged running pipeline command.
                if (!StopPipeline(runningCmd))
                {
                    Executor.CancelCurrentExecutor();
                }
            }

            if (ShouldEndSession)
                Runspace?.Close();

            // call the console APIs directly, instead of ui.rawui.FlushInputHandle, as ui may be finalized
            // already if this thread is lagging behind the main thread.

            terminal.FlushConsoleInputBuffer();

            breakHandlerThread = null;
        }

        #endregion
        #region Exit

        private bool setShouldExitCalled;
        private bool noExit = true;

        internal int ExitCode;
        private int exitCodeFromRunspace;

        internal const int ExitCodeSuccess = 0;
        internal const int ExitCodeCtrlBreak = 128 + 21; // SIGBREAK
        internal const int ExitCodeInitFailure = 70; // Internal Software Error
        internal const int ExitCodeBadCommandLineParameter = 64; // Command Line Usage Error

        internal bool ShouldEndSession
        {
            // This might get called from the main thread, or from the pipeline thread, or from a break handler thread.
            get
            {
                bool result = false;

                lock (hostGlobalLock)
                {
                    result = shouldEndSession;
                }

                return result;
            }
            set
            {
                lock (hostGlobalLock)
                {
                    // If ShouldEndSession is already true, you can't set it back

                    Debug.Assert(!shouldEndSession || value, "ShouldEndSession can only be set from false to true");

                    shouldEndSession = value;
                }
            }
        }

        /// <summary>
        /// Request by the engine to end the current engine runspace (to shut down and terminate the host's root runspace).
        /// </summary>
        /// <param name="exitCode">The exit code accompanying the exit keyword. Typically, after exiting a runspace, a host will also
        /// terminate. The exitCode parameter can be used to set the host's process exit code.</param>
        public override void SetShouldExit(int exitCode)
        {
            lock (hostGlobalLock)
            {
                // Check for the pushed runspace scenario.
                if (this.IsRunspacePushed)
                {
                    this.PopRunspace();
                }
                else if (InDebugMode)
                {
                    ExitDebugMode(DebuggerResumeAction.Continue);
                }
                else
                {
                    setShouldExitCalled = true;
                    exitCodeFromRunspace = exitCode;
                    ShouldEndSession = true;
                }
            }
        }

        #endregion
        #region Error

        private void ReportException(Exception e, Executor exec)
        {
            Debug.Assert(e != null, "must supply an Exception");
            Debug.Assert(exec != null, "must supply an Executor");

            // NTRAID#Windows Out Of Band Releases-915506-2005/09/09
            // Removed HandleUnexpectedExceptions infrastructure

            // Attempt to write the exception into the error stream so that the normal F&O machinery will
            // display it according to preferences.

            object error = null;
            Pipeline tempPipeline = Runspace.CreatePipeline();

            // NTRAID#Windows OS Bugs-1143621-2005/04/08-sburns

            if (e is IContainsErrorRecord icer)
            {
                error = icer.ErrorRecord;
            }
            else
            {
                error = (object) new ErrorRecord(e, "ConsoleHost.ReportException", ErrorCategory.NotSpecified, null);
        }

            /*PSObject wrappedError = new PSObject(error)
            {
                WriteStream = WriteStreamType.Error
            };*/
            PSObject wrappedError = null;
            throw new NotImplementedException();

            Exception e1 = null;

            tempPipeline.Input.Write(wrappedError);

            exec.ExecutePipeline(tempPipeline, out e1, Executor.ExecutionOptions.AddOutDefault);

            if (e1 != null)
            {
                // that didn't work.  Write out the error ourselves as a last resort.

                ReportExceptionFallback(e, null);
            }
        }

        /// <summary>
        /// Reports an exception according to the exception reporting settings in effect.
        /// </summary>
        /// <param name="e">
        /// The exception to report.
        /// </param>
        /// <param name="header">
        /// Optional header message.  Empty or null means "no header"
        /// </param>
        private void ReportExceptionFallback(Exception e, string header)
        {
            if (!string.IsNullOrEmpty(header))
            {
                Console.Error.WriteLine(header);
            }

            if (e == null)
            {
                return;
            }

            // See if the exception has an error record attached to it...
            ErrorRecord er = null;
            if (e is IContainsErrorRecord icer)
                er = icer.ErrorRecord;

            if (e is PSRemotingTransportException)
            {
                // For remoting errors use full fidelity error writer.
                UI.WriteErrorLine(e.Message);
            }
            else if (e is TargetInvocationException)
            {
                Console.Error.WriteLine(e.InnerException.Message);
            }
            else
            {
                Console.Error.WriteLine(e.Message);
            }

            // Add the position message for the error if it's available.
            if (er != null && er.InvocationInfo != null)
                Console.Error.WriteLine(er.InvocationInfo.PositionMessage);
        }

        #endregion
        #region Application

        /// <summary>
        /// Called by the engine to notify the host that it is about to execute a "legacy" command line application.
        /// A legacy application is defined as a console-mode executable that may do one or more of the following:
        /// 
        /// * reads from stdin
        /// * writes to stdout
        /// * writes to stderr
        /// * uses any of the win32 console APIs.
        /// </summary>
        public override void NotifyBeginApplication()
        {
            //PowerShell uses this to save the console title so that it can be reverted later
        }

        public override void NotifyEndApplication()
        {
            //PowerShell uses this to revert the console title
        }

        #endregion
    }
}
