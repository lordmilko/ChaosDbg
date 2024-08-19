using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Security;
using System.Text;
using ChaosDbg.Terminal;
using ChaosLib;
using SMA = System.Management.Automation;

namespace ChaosDbg.PowerShell.Host
{
    //Based on https://github.com/PowerShell/PowerShell/blob/da1ca4a7266e2c9e43f96a38aeea8092ace71cca/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHostUserInterface.cs

    /// <summary>
    /// Defines the properties and facilities providing by an hosting application deriving from <see cref="PSHost"/> that offers dialog-oriented
    /// and line-oriented interactive features.
    /// </summary>
    abstract class PSHostUserInterfaceBase : PSHostUserInterface
    {
        /// <summary>
        /// Gets hosting application's implementation of the <see cref="PSHostRawUserInterface"/> abstract base class that implements that class.
        /// </summary>
        public override PSHostRawUserInterface RawUI { get; }

        protected PSHostBase host;
        protected ITerminal terminal;
        private SMA.PowerShell commandCompletionPowerShell;
        private ProgressPane progressPane;

        private const int MaxInputLineLength = 1024;
        private const string Tab = "\x0009";

        internal bool NoPrompt { get; set; }

        internal bool ReadFromStdin { get; set; }

        /// <summary>
        /// True if command completion is currently running.
        /// </summary>
        internal bool IsCommandCompletionRunning =>
            commandCompletionPowerShell != null &&
            commandCompletionPowerShell.InvocationStateInfo.State == PSInvocationState.Running;

        private static MethodInfo CommandCompletion_CompleteInputInDebugger = typeof(CommandCompletion).GetMethodInfo("CompleteInputInDebugger", new[]{typeof(string), typeof(int), typeof(Hashtable), typeof(SMA.Debugger)});
        private static MethodInfo PowerShell_SetIsNested = typeof(SMA.PowerShell).GetMethodInfo("SetIsNested");

        public ConsoleColor DebugForegroundColor { get; set; } = ConsoleColor.Yellow;

        public ConsoleColor DebugBackgroundColor { get; set; } = Console.BackgroundColor;

        protected PSHostUserInterfaceBase(PSHostBase host, ITerminal terminal)
        {
            this.host = host;
            this.terminal = terminal;
            RawUI = new ChaosHostRawUserInterface(terminal);
        }

        #region ReadLineWithTabCompletion

        internal string ReadLineWithTabCompletion()
        {
            //From https://github.com/PowerShell/PowerShell/blob/da1ca4a7266e2c9e43f96a38aeea8092ace71cca/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHostUserInterface.cs#L1964

            string input;
            string lastInput = string.Empty;
            ReadLineResult rlResult;

            Size screenBufferSize = RawUI.BufferSize;
            var endOfPromptCursorPos = RawUI.CursorPosition;

            string lastCompletion = string.Empty;
            CommandCompletion commandCompletion = null;
            string completionInput = null;

            while (true)
            {
                if (TryInvokeUserDefinedReadLine(out input))
                    break;

                input = ReadLine(true, lastInput, out rlResult, false);

                if (input == null)
                    break;

                if (rlResult == ReadLineResult.endedOnEnter)
                {
                    break;
                }

                var endOfInputCursorPos = RawUI.CursorPosition;
                string completedInput;

                if (rlResult == ReadLineResult.endedOnTab || rlResult == ReadLineResult.endedOnShiftTab)
                {
                    int tabIndex = input.IndexOf(Tab, StringComparison.Ordinal);
                    Debug.Assert(tabIndex != -1, "tab should appear in the input");

                    string restOfLine = string.Empty;
                    int leftover = input.Length - tabIndex - 1;

                    if (leftover > 0)
                    {
                        // We are reading from the console (not redirected, b/c we don't end on tab when redirected)
                        // If the cursor is at the end of a line, there is actually a space character at the cursor's position and when we type tab
                        // at the end of a line, that space character is replaced by the tab. But when we type tab at the middle of a line, the space
                        // character at the end is preserved, we should remove that space character because it's not provided by the user.
                        input = input.Remove(input.Length - 1);
                        restOfLine = input.Substring(tabIndex + 1);
                    }

                    input = input.Remove(tabIndex);

                    if (input != lastCompletion || commandCompletion == null)
                    {
                        completionInput = input;
                        commandCompletion = GetNewCompletionResults(input);
                    }

                    var completionResult = commandCompletion.GetNextResult(rlResult == ReadLineResult.endedOnTab);

                    if (completionResult != null)
                        completedInput = string.Concat(completionInput.Substring(0, commandCompletion.ReplacementIndex), completionResult.CompletionText);
                    else
                        completedInput = completionInput;

                    if (restOfLine != string.Empty)
                        completedInput += restOfLine;

                    if (completedInput.Length > (MaxInputLineLength - 2))
                        completedInput = completedInput.Substring(0, MaxInputLineLength - 2);

                    // Remove any nulls from the string...
                    completedInput = RemoveNulls(completedInput);

                    // adjust the saved cursor position if the buffer scrolled as the user was typing (i.e. the user
                    // typed past the end of the buffer).

                    int linesOfInput = (endOfPromptCursorPos.X + input.Length) / screenBufferSize.Width;
                    endOfPromptCursorPos.Y = endOfInputCursorPos.Y - linesOfInput;

                    // replace the displayed input with the new input
                    try
                    {
                        RawUI.CursorPosition = endOfPromptCursorPos;
                    }
                    catch (PSArgumentOutOfRangeException)
                    {
                        // If we go a range exception, it's because
                        // there's no room in the buffer for the completed
                        // line so we'll just pretend that there was no match...
                        break;
                    }

                    // When the string is written to the console, a space character is actually appended to the string
                    // and the cursor will flash at the position of that space character.
                    WriteToConsole(completedInput, false);

                    var endOfCompletionCursorPos = RawUI.CursorPosition;

                    // adjust the starting cursor position if the screen buffer has scrolled as a result of writing the
                    // completed input (i.e. writing the completed input ran past the end of the buffer).

                    int linesOfCompletedInput = (endOfPromptCursorPos.X + completedInput.Length) / screenBufferSize.Width;
                    endOfPromptCursorPos.Y = endOfCompletionCursorPos.Y - linesOfCompletedInput;

                    // blank out any "leftover" old input.  That's everything between the cursor position at the time
                    // the user hit tab up to the current cursor position after writing the completed text.

                    int deltaInput =
                        (endOfInputCursorPos.Y * screenBufferSize.Width + endOfInputCursorPos.X)
                        - (endOfCompletionCursorPos.Y * screenBufferSize.Width + endOfCompletionCursorPos.X);

                    if (deltaInput > 0)
                    {
                        terminal.FillConsoleOutputCharacter(' ', deltaInput, new COORD
                        {
                            X = (short) endOfCompletionCursorPos.X,
                            Y = (short) endOfCompletionCursorPos.Y
                        });
                    }

                    if (restOfLine != string.Empty)
                    {
                        lastCompletion = completedInput.Remove(completedInput.Length - restOfLine.Length);
                        SendLeftArrows(restOfLine.Length);
                    }
                    else
                    {
                        lastCompletion = completedInput;
                    }

                    lastInput = completedInput;
                }
            }

            return input;
        }

        private CommandCompletion GetNewCompletionResults(string input)
        {
            try
            {
                var runspace = host.Runspace;
                var debugger = runspace.Debugger;

                if ((debugger != null) && debugger.InBreakpoint)
                {
                    // If in debug stop mode do command completion though debugger process command.
                    try
                    {
                        //The regular CompleteInput method defers to this, but requires a PowerShell, which our root runspace doesn't have
                        return (CommandCompletion) CommandCompletion_CompleteInputInDebugger.Invoke(null, new object[]{input, input.Length, null, debugger});
                    }
                    catch (PSInvalidOperationException)
                    {
                    }
                }

                if (runspace.GetType().Name == "LocalRunspace" && GetNestedPromptCount() > 0)
                {
                    commandCompletionPowerShell = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
                }
                else
                {
                    commandCompletionPowerShell = SMA.PowerShell.Create();
                    PowerShell_SetIsNested.Invoke(commandCompletionPowerShell, new object[] {host.IsNested});
                    commandCompletionPowerShell.Runspace = runspace;
                }

                return CommandCompletion.CompleteInput(input, input.Length, null, commandCompletionPowerShell);
            }
            finally
            {
                commandCompletionPowerShell = null;
            }
        }

        private void SendLeftArrows(int length)
        {
            var inputs = new INPUT[length * 2];

            for (var i = 0; i < length; i++)
            {
                var pushDown = new INPUT
                {
                    Type = InputType.KEYBOARD,
                    Input =
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VirtualKey.Left,
                            wScan = 0,
                            dwFlags = KEYEVENTF.NONE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                var letUp = new INPUT
                {
                    Type = InputType.KEYBOARD,
                    Input =
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VirtualKey.Left,
                            wScan = 0,
                            dwFlags = KEYEVENTF.KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                inputs[2 * i] = pushDown;
                inputs[2 * i + 1] = letUp;
            }

            terminal.SendInput(inputs, false);
        }

        /// <summary>
        /// Strip nulls from a string.
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <returns>The string with any '\0' characters removed.</returns>
        private static string RemoveNulls(string input)
        {
            if (!input.Contains('\0'))
                return input;

            var sb = new StringBuilder(input.Length);

            foreach (var c in input)
            {
                if (c != '\0')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private const string CustomReadLineCommand = "PSConsoleHostReadLine";

        private bool TryInvokeUserDefinedReadLine(out string input)
        {
            //Try and use PSReadLine to read the input

            var executionContext = host.Runspace.GetPropertyValue("Engine").GetPropertyValue("Context");

            var intrinsics = (EngineIntrinsics) executionContext.GetPropertyValue("EngineIntrinsics");

            CommandInfo psReadLineCommand;

            if (host.Runspace.GetType().Name == "LocalRunspace" && (psReadLineCommand = intrinsics.InvokeCommand.GetCommands(CustomReadLineCommand, CommandTypes.Function | CommandTypes.Cmdlet, nameIsPattern: false).FirstOrDefault()) != null)
            {
                var nestedPromptCount = GetNestedPromptCount();

                SMA.PowerShell ps;

                if (nestedPromptCount > 0 && Runspace.DefaultRunspace != null)
                    ps = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
                else
                {
                    ps = SMA.PowerShell.Create();
                    ps.Runspace = host.Runspace;
                }

                if (TryInvokePSReadLineRaw(psReadLineCommand, intrinsics, out input))
                    return true;

                var results = ps.AddCommand(CustomReadLineCommand).Invoke();

                if (results.Count == 1)
                {
                    input = results[0]?.BaseObject as string;
                    return true;
                }
            }

            input = null;
            return false;
        }

        protected abstract bool TryInvokePSReadLineRaw(CommandInfo psReadLineCommand, EngineIntrinsics engineIntrinsics, out string input);

        private int GetNestedPromptCount()
        {
            return (int) host.Runspace.GetPropertyValue("ExecutionContext").GetPropertyValue("EngineHostInterface").GetPropertyValue("NestedPromptCount");
        }

        #endregion

        internal string ReadLine(bool endOnTab, string initialContent, out ReadLineResult result, bool calledFromPipeline)
        {
            result = ReadLineResult.endedOnEnter;

            string restOfLine = null;

            string s = ReadFromStdin
                ? ReadLineFromFile(initialContent)
                : ReadLineFromConsole(endOnTab, initialContent, calledFromPipeline, ref restOfLine, ref result);

            PostRead();

            if (restOfLine != null)
                s += restOfLine;

            return s;
        }

        private string ReadLineFromConsole(bool endOnTab, string initialContent, bool calledFromPipeline, ref string restOfLine, ref ReadLineResult result)
        {
            PreRead();
            // Ensure that we're in the proper line-input mode.

            ConsoleMode m = terminal.GetInputConsoleMode();

            const ConsoleMode DesiredMode =
                ConsoleMode.ENABLE_LINE_INPUT
                | ConsoleMode.ENABLE_ECHO_INPUT
                | ConsoleMode.ENABLE_PROCESSED_INPUT;

            if ((m & DesiredMode) != DesiredMode || (m & ConsoleMode.ENABLE_MOUSE_INPUT) > 0)
            {
                m &= ~ConsoleMode.ENABLE_MOUSE_INPUT;
                m |= DesiredMode;
                terminal.SetInputConsoleMode(m);
            }

            // If more characters are typed than you asked, then the next call to ReadConsole will return the
            // additional characters beyond those you requested.
            //
            // If input is terminated with a tab key, then the buffer returned will have a tab (ascii 0x9) at the
            // position where the tab key was hit.  If the user has arrowed backward over existing input in the line
            // buffer, the tab will overwrite whatever character was in that position. That character will be lost in
            // the input buffer, but since we echo each character the user types, it's still in the active screen buffer
            // and we can read the console output to get that character.
            //
            // If input is terminated with an enter key, then the buffer returned will have ascii 0x0D and 0x0A
            // (Carriage Return and Line Feed) as the last two characters of the buffer.
            //
            // If input is terminated with a break key (Ctrl-C, Ctrl-Break, Close, etc.), then the buffer will be
            // the empty string.
            ((ChaosHostRawUserInterface) RawUI).ClearKeyCache();
            int keyState = 0;
            string s = string.Empty;
            System.Span<char> inputBuffer = stackalloc char[MaxInputLineLength + 1]; //Stepping over this will crash
            if (initialContent.Length > 0)
            {
                initialContent.AsSpan().CopyTo(inputBuffer);
            }

            while (true)
            {
                s += terminal.ReadConsole(initialContent.Length, inputBuffer, MaxInputLineLength, endOnTab, out keyState);
                Debug.Assert(s != null, "s should never be null");

                if (s.Length == 0)
                {
                    result = ReadLineResult.endedOnBreak;
                    s = null;

                    if (calledFromPipeline)
                    {
                        // make sure that the pipeline that called us is stopped

                        throw new PipelineStoppedException();
                    }

                    break;
                }

                if (s.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                {
                    result = ReadLineResult.endedOnEnter;

                    s = s.Remove(s.Length - Environment.NewLine.Length);
                    break;
                }

                int i = s.IndexOf(Tab, StringComparison.Ordinal);

                if (endOnTab && i != -1)
                {
                    // then the tab we found is the completion character.  bit 0x10 is set if the shift key was down
                    // when the key was hit.

                    if ((keyState & 0x10) == 0)
                    {
                        result = ReadLineResult.endedOnTab;
                    }
                    else if ((keyState & 0x10) > 0)
                    {
                        result = ReadLineResult.endedOnShiftTab;
                    }
                    else
                    {
                        // do nothing: leave the result state as it was. This is the circumstance when we've have to
                        // do more than one iteration and the input ended on a tab or shift-tab, or the user hit
                        // enter, or the user hit ctrl-c
                    }

                    // also clean up the screen -- if the cursor was positioned somewhere before the last character
                    // in the input buffer, then the characters from the tab to the end of the buffer need to be
                    // erased.
                    int leftover = RawUI.LengthInBufferCells(s.Substring(i + 1));

                    if (leftover > 0)
                    {
                        Coordinates c = RawUI.CursorPosition;

                        // before cleaning up the screen, read the active screen buffer to retrieve the character that
                        // is overridden by the tab
                        char charUnderCursor = GetCharacterUnderCursor(c);

                        Write(new string(' ', leftover));
                        RawUI.CursorPosition = c;

                        restOfLine = s[i] + (charUnderCursor + s.Substring(i + 1));
                    }
                    else
                    {
                        restOfLine += s[i];
                    }

                    s = s.Remove(i);

                    break;
                }
            }

            Debug.Assert((s == null && result == ReadLineResult.endedOnBreak)
                       || (s != null && result != ReadLineResult.endedOnBreak),
                       "s should only be null if input ended with a break");

            return s;
        }

        /// <summary>
        /// Get the character at the cursor when the user types 'tab' in the middle of line.
        /// </summary>
        /// <param name="cursorPosition">The cursor position where 'tab' is hit.</param>
        /// <returns></returns>
        private char GetCharacterUnderCursor(Coordinates cursorPosition)
        {
            Rectangle region = new Rectangle(0, cursorPosition.Y, RawUI.BufferSize.Width - 1, cursorPosition.Y);
            BufferCell[,] content = RawUI.GetBufferContents(region);

            for (int index = 0, column = 0; column <= cursorPosition.X; index++)
            {
                BufferCell cell = content[0, index];
                if (cell.BufferCellType == BufferCellType.Complete || cell.BufferCellType == BufferCellType.Leading)
                {
                    if (column == cursorPosition.X)
                    {
                        return cell.Character;
                    }

                    column += ConsoleTerminal.LengthInBufferCells(cell.Character);
                }
            }

            Debug.Assert(false, "the character at the cursor should be retrieved, never gets to here");
            return '\0';
        }

        private string ReadLineFromFile(string initialContent)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(initialContent))
            {
                sb.Append(initialContent);
                sb.Append('\n');
            }

            var consoleIn = Console.In;
            while (true)
            {
                var inC = consoleIn.Read();
                if (inC == -1)
                {
                    // EOF - we return null which tells our caller to exit
                    // but only if we don't have any input, we could have
                    // input and then stdin was closed, but never saw a newline.
                    return sb.Length == 0 ? null : sb.ToString();
                }

                var c = unchecked((char) inC);

                if (!NoPrompt)
                {
                    Console.Out.Write(c);
                }

                if (c == '\r')
                {
                    // Treat as newline, but consume \n if there is one.
                    if (consoleIn.Peek() == '\n')
                    {
                        if (!NoPrompt)
                        {
                            Console.Out.Write('\n');
                        }

                        consoleIn.Read();
                    }

                    break;
                }

                if (c == '\n')
                {
                    break;
                }

                // If NoPrompt is true, we are in a sort of server mode where we shouldn't
                // do anything like edit the command line - every character is part of the input.
                if (c == '\b' && !NoPrompt)
                {
                    sb.Remove(sb.Length - 1, 1);
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        internal void ResetProgress()
        {
            //todo
        }

        #region WriteToConsole

        private void WriteToConsole(string value, bool newLine = false)
        {
            // Ensure that we're in the proper line-output mode.  We don't lock here as it does not matter if we
            // attempt to set the mode from multiple threads at once.
            var m = terminal.GetOutputConsoleMode();

            const ConsoleMode DesiredMode = ConsoleMode.ENABLE_PROCESSED_OUTPUT | ConsoleMode.ENABLE_WRAP_AT_EOL_OUTPUT;

            if ((m & DesiredMode) != DesiredMode)
            {
                m |= DesiredMode;
                terminal.SetOutputConsoleMode(m);
            }

            PreWrite();

            // This is atomic, so we don't lock here...
            terminal.WriteConsole(value, newLine);

            PostWrite();
        }

        private void PreRead() => progressPane?.Hide();

        private void PostRead() => progressPane?.Show();

        private void PreWrite() => progressPane?.Hide();

        private void PostWrite() => progressPane?.Show();

        #endregion
        #region Write

        public override void Write(string value)
        {
            terminal.WriteConsole(value, false);
        }

        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) =>
            Write(foregroundColor, backgroundColor, value + "\n");

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            terminal.LockProtection(() =>
            {
                var fg = RawUI.ForegroundColor;
                var bg = RawUI.BackgroundColor;

                RawUI.ForegroundColor = foregroundColor;
                RawUI.BackgroundColor = backgroundColor;

                try
                {
                    terminal.WriteConsole(value, false);
                }
                finally
                {
                    RawUI.ForegroundColor = fg;
                    RawUI.BackgroundColor = bg;
                }
            });
        }

        public override void WriteLine(string value)
        {
            terminal.WriteConsole(value, true);
        }

        public override void WriteErrorLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            WriteLine(ConsoleColor.Red, Console.BackgroundColor, value);
        }

        public override void WriteDebugLine(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            //throw new NotImplementedException();
        }

        public override void WriteVerboseLine(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteWarningLine(string message)
        {
            throw new NotImplementedException();
        }

        #endregion
        #region Read

        public override string ReadLine()
        {
            throw new NotImplementedException();
        }

        public override SecureString ReadLineAsSecureString()
        {
            throw new NotImplementedException();
        }

        #endregion
        #region Prompt

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName,
            PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            throw new NotImplementedException();
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            throw new NotImplementedException();
        }

        #endregion

        internal enum ReadLineResult
        {
            endedOnEnter = 0,
            endedOnTab = 1,
            endedOnShiftTab = 2,
            endedOnBreak = 3
        }
    }
}
