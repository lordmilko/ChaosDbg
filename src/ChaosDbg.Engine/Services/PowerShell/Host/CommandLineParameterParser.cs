using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Text;

#nullable enable

namespace ChaosDbg.PowerShell.Host
{
    //From https://github.com/PowerShell/PowerShell/blob/master/src/Microsoft.PowerShell.ConsoleHost/host/msh/CommandLineParameterParser.cs

    static class CommandLineParameterParserStrings
    {
        public const string NullElementInArgs = "The specified arguments must not contain null elements.";
        public const string MissingCustomPipeNameArgument = "Cannot process the command because -CustomPipeName requires an argument that is a name of the pipe you want to use. Specify this argument and try again.";
        public const string MissingWindowStyleArgument = "Cannot process the command because -WindowStyle requires an argument that is normal, hidden, minimized or maximized. Specify one of these argument values and try again.";
        public const string InvalidWindowStyleArgument = "Processing -WindowStyle '{0}' failed: {1}.";
        public const string MissingExecutionPolicyParameter = "Cannot process the execution policy because of a missing policy name. A policy name must follow -ExecutionPolicy.";
        public const string InvalidExecutionPolicyArgument = "Invalid ExecutionPolicy value '{0}'.";
        public const string MissingWorkingDirectoryArgument = "Cannot process the command because -WorkingDirectory requires an argument that is a directory path.";
        public const string MissingFileArgument = "The command cannot be run because the File parameter requires a file path. Supply a path for the File parameter and then try the command again.";
        public const string InvalidFileArgument = "Processing -File '{0}' failed: {1} Specify a valid path for the -File parameter";
        public const string InvalidArgument = "Invalid argument '{0}', did you mean:";
        public const string ArgumentFileDoesNotExist = "The argument '{0}' is not recognized as the name of a script file. Check the spelling of the name, or if a path was included, verify that the path is correct and try again.";
        public const string InvalidFileArgumentExtension = "Processing -File '{0}' failed because the file does not have a '.ps1' extension. Specify a valid PowerShell script file name, and then try again.";
        public const string CommandAlreadySpecified = "Cannot process command because a command is already specified with -Command, -CommandWithArgs, or -EncodedCommand.";
        public const string MissingCommandParameter = "Cannot process the command because of a missing parameter. A command must follow -Command.";
        public const string BadCommandValue = "Cannot process the command because the value specified with -EncodedCommand is not properly encoded. The value must be Base64 encoded.";
        public const string TooManyParametersToCommand = "'-' was specified with the -Command parameter; no other arguments to -Command are permitted.";
        public const string StdinNotRedirected = "'-' was specified as the argument to -Command but standard input has not been redirected for this process.";
        public const string ArgsAlreadySpecified = "Cannot process the command because arguments to -Command or -EncodedCommand have already been specified with -EncodedArguments.";
        public const string MissingArgsValue = "Cannot process the command because -EncodedArguments requires a value. Specify a value for the -EncodedArguments parameter.";
        public const string BadArgsValue = "Cannot process the command because the value specified with -EncodedArguments is not properly encoded. The value must be Base64 encoded.";
    }

    internal class CommandLineParameterParser
    {
        internal static int MaxNameLength() => ushort.MaxValue;

        internal bool? InputRedirectedTestHook;

        private static readonly string[] s_validParameters = {
            "command",
            "commandwithargs",
            "custompipename",
            "encodedcommand",
            "executionpolicy",
            "file",
            "help",
            "login",
            "noexit",
            "nologo",
            "noninteractive",
            "noprofile",
            "noprofileloadtime",
            "removeworkingdirectorytrailingcharacter",
            "settingsfile",
            "version",
            "windowstyle",
            "workingdirectory"
        };

        /// <summary>
        /// These represent the parameters that are used when starting pwsh.
        /// We can query in our telemetry to determine how pwsh was invoked.
        /// </summary>
        [Flags]
        internal enum ParameterBitmap : long
        {
            Command             = 0x00000001, // -Command | -c
            ConfigurationName   = 0x00000002, // -ConfigurationName | -config
            CustomPipeName      = 0x00000004, // -CustomPipeName
            EncodedCommand      = 0x00000008, // -EncodedCommand | -e | -ec
            EncodedArgument     = 0x00000010, // -EncodedArgument
            ExecutionPolicy     = 0x00000020, // -ExecutionPolicy | -ex | -ep
            File                = 0x00000040, // -File | -f
            Help                = 0x00000080, // -Help, -?, /?
            InputFormat         = 0x00000100, // -InputFormat | -inp | -if
            Interactive         = 0x00000200, // -Interactive | -i
            Login               = 0x00000400, // -Login | -l
            MTA                 = 0x00000800, // -MTA
            NoExit              = 0x00001000, // -NoExit | -noe
            NoLogo              = 0x00002000, // -NoLogo | -nol
            NonInteractive      = 0x00004000, // -NonInteractive | -noni
            NoProfile           = 0x00008000, // -NoProfile | -nop
            OutputFormat        = 0x00010000, // -OutputFormat | -o | -of
            SettingsFile        = 0x00020000, // -SettingsFile | -settings
            SSHServerMode       = 0x00040000, // -SSHServerMode | -sshs
            SocketServerMode    = 0x00080000, // -SocketServerMode | -sockets
            ServerMode          = 0x00100000, // -ServerMode | -server
            NamedPipeServerMode = 0x00200000, // -NamedPipeServerMode | -namedpipes
            STA                 = 0x00400000, // -STA
            Version             = 0x00800000, // -Version | -v
            WindowStyle         = 0x01000000, // -WindowStyle | -w
            WorkingDirectory    = 0x02000000, // -WorkingDirectory | -wd
            ConfigurationFile   = 0x04000000, // -ConfigurationFile
            NoProfileLoadTime   = 0x08000000, // -NoProfileLoadTime
            CommandWithArgs     = 0x10000000, // -CommandWithArgs | -cwa
            // Enum values for specified ExecutionPolicy
            EPUnrestricted      = 0x0000000100000000, // ExecutionPolicy unrestricted
            EPRemoteSigned      = 0x0000000200000000, // ExecutionPolicy remote signed
            EPAllSigned         = 0x0000000400000000, // ExecutionPolicy all signed
            EPRestricted        = 0x0000000800000000, // ExecutionPolicy restricted
            EPDefault           = 0x0000001000000000, // ExecutionPolicy default
            EPBypass            = 0x0000002000000000, // ExecutionPolicy bypass
            EPUndefined         = 0x0000004000000000, // ExecutionPolicy undefined
            EPIncorrect         = 0x0000008000000000, // ExecutionPolicy incorrect
        }

        internal ParameterBitmap ParametersUsed = 0;

        [Conditional("DEBUG")]
        private void AssertArgumentsParsed()
        {
            if (!_dirty)
            {
                throw new InvalidOperationException("Parse has not been called yet");
            }
        }

        private bool _socketServerMode;
        private bool _serverMode;
        private bool _namedPipeServerMode;
        private bool _sshServerMode;
        private bool _noProfileLoadTime;
        private bool _showVersion;
        private string? _error;
        private bool _showHelp;
        private bool _showExtendedHelp;
        private bool _showBanner = true;
        private bool _noInteractive;
        private bool _abortStartup;
        private bool _skipUserInit;
        private string? _customPipeName;
        private bool _noExit = true;
        private bool _explicitReadCommandsFromStdin;
        private bool _noPrompt;
        private string? _commandLineCommand;
        private bool _wasCommandEncoded;
        private bool _commandHasArgs;
        private uint _exitCode = PSHostBase.ExitCodeSuccess;
        private bool _dirty;
        private readonly Collection<CommandParameter> _collectedArgs = new Collection<CommandParameter>();
        private string? _file;
        private string? _executionPolicy;
        private string? _settingsFile;
        private string? _workingDirectory;
        private ProcessWindowStyle? _windowStyle;
        private bool _removeWorkingDirectoryTrailingCharacter = false;

        internal CommandLineParameterParser()
        {
        }

        #region Internal properties

        internal bool AbortStartup
        {
            get
            {
                AssertArgumentsParsed();
                return _abortStartup;
            }
        }

        internal string? SettingsFile
        {
            get
            {
                AssertArgumentsParsed();
                return _settingsFile;
            }
        }

        internal string? InitialCommand
        {
            get
            {
                AssertArgumentsParsed();
                return _commandLineCommand;
            }
        }

        internal bool WasInitialCommandEncoded
        {
            get
            {
                AssertArgumentsParsed();
                return _wasCommandEncoded;
            }
        }

        internal ProcessWindowStyle? WindowStyle
        {
            get
            {
                AssertArgumentsParsed();
                return _windowStyle;
            }
        }

        internal bool ShowBanner
        {
            get
            {
                AssertArgumentsParsed();
                return _showBanner;
            }
        }

        internal bool NoExit
        {
            get
            {
                AssertArgumentsParsed();
                return _noExit;
            }
        }

        internal bool SkipProfiles
        {
            get
            {
                AssertArgumentsParsed();
                return _skipUserInit;
            }
        }

        internal uint ExitCode
        {
            get
            {
                AssertArgumentsParsed();
                return _exitCode;
            }
        }

        internal bool ExplicitReadCommandsFromStdin
        {
            get
            {
                AssertArgumentsParsed();
                return _explicitReadCommandsFromStdin;
            }
        }

        internal bool NoPrompt
        {
            get
            {
                AssertArgumentsParsed();
                return _noPrompt;
            }
        }

        internal Collection<CommandParameter> Args
        {
            get
            {
                AssertArgumentsParsed();
                return _collectedArgs;
            }
        }

        internal bool SocketServerMode
        {
            get
            {
                AssertArgumentsParsed();
                return _socketServerMode;
            }
        }

        internal bool NamedPipeServerMode
        {
            get
            {
                AssertArgumentsParsed();
                return _namedPipeServerMode;
            }
        }

        internal bool SSHServerMode
        {
            get
            {
                AssertArgumentsParsed();
                return _sshServerMode;
            }
        }

        internal bool ServerMode
        {
            get
            {
                AssertArgumentsParsed();
                return _serverMode;
            }
        }

        // Added for using in xUnit tests
        internal string? ErrorMessage
        {
            get
            {
                AssertArgumentsParsed();
                return _error;
            }
        }

        // Added for using in xUnit tests
        internal bool ShowShortHelp
        {
            get
            {
                AssertArgumentsParsed();
                return _showHelp;
            }
        }

        // Added for using in xUnit tests
        internal bool ShowExtendedHelp
        {
            get
            {
                AssertArgumentsParsed();
                return _showExtendedHelp;
            }
        }

        internal bool NoProfileLoadTime
        {
            get
            {
                AssertArgumentsParsed();
                return _noProfileLoadTime;
            }
        }

        internal bool ShowVersion
        {
            get
            {
                AssertArgumentsParsed();
                return _showVersion;
            }
        }

        internal string? CustomPipeName
        {
            get
            {
                AssertArgumentsParsed();
                return _customPipeName;
            }
        }

        internal string? File
        {
            get
            {
                AssertArgumentsParsed();
                return _file;
            }
        }

        internal string? ExecutionPolicy
        {
            get
            {
                AssertArgumentsParsed();
                return _executionPolicy;
            }
        }

        internal bool ThrowOnReadAndPrompt
        {
            get
            {
                AssertArgumentsParsed();
                return _noInteractive;
            }
        }

        internal bool NonInteractive
        {
            get
            {
                AssertArgumentsParsed();
                return _noInteractive;
            }
        }

        internal string? WorkingDirectory
        {
            get
            {
                AssertArgumentsParsed();
#if !UNIX
                if (_removeWorkingDirectoryTrailingCharacter && _workingDirectory?.Length > 0)
                {
                    return _workingDirectory.Remove(_workingDirectory.Length - 1);
                }
#endif
                return _workingDirectory;
            }
        }

#if !UNIX
        internal bool RemoveWorkingDirectoryTrailingCharacter
        {
            get
            {
                AssertArgumentsParsed();
                return _removeWorkingDirectoryTrailingCharacter;
            }
        }
#endif

        #endregion Internal properties
        #region static methods

        /// <summary>
        /// Gets the word in a switch from the current argument or parses a file.
        /// For example -foo, /foo, or --foo would return 'foo'.
        /// </summary>
        /// <param name="args">
        /// The command line parameters to be processed.
        /// </param>
        /// <param name="argIndex">
        /// The index in args to the argument to process.
        /// </param>
        /// <param name="noexitSeen">
        /// Used during parsing files.
        /// </param>
        /// <returns>
        /// Returns a Tuple:
        /// The first value is a String called 'switchKey' with the word in a switch from the current argument or null.
        /// The second value is a bool called 'shouldBreak', indicating if the parsing look should break.
        /// </returns>
        private (string switchKey, bool shouldBreak) GetSwitchKey(string[] args, ref int argIndex, ref bool noexitSeen)
        {
            string switchKey = args[argIndex].Trim();
            if (string.IsNullOrEmpty(switchKey))
            {
                return (switchKey: string.Empty, shouldBreak: false);
            }

            char firstChar = switchKey[0];
            if (firstChar != '-' && firstChar != '/')
            {
                // then it's a file
                --argIndex;
                ParseFile(args, ref argIndex, noexitSeen);

                return (switchKey: string.Empty, shouldBreak: true);
            }

            // chop off the first character so that we're agnostic wrt specifying / or -
            // in front of the switch name.
            switchKey = switchKey.Substring(1);

            // chop off the second dash so we're agnostic wrt specifying - or --
            if (!string.IsNullOrEmpty(switchKey) && firstChar == '-' && switchKey[0] == firstChar)
            {
                switchKey = switchKey.Substring(1);
            }

            return (switchKey: switchKey, shouldBreak: false);
        }

        internal static string NormalizeFilePath(string path)
        {
            // Normalize slashes
            path = path.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar);

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Determine the execution policy based on the supplied string.
        /// If the string doesn't match to any known execution policy, set it to incorrect.
        /// </summary>
        /// <param name="_executionPolicy">The value provided on the command line.</param>
        /// <returns>The execution policy.</returns>
        private static ParameterBitmap GetExecutionPolicy(string? _executionPolicy)
        {
            if (_executionPolicy is null)
            {
                return ParameterBitmap.EPUndefined;
            }

            ParameterBitmap executionPolicySetting = ParameterBitmap.EPIncorrect;

            if (string.Equals(_executionPolicy, "default", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPDefault;
            }
            else if (string.Equals(_executionPolicy, "remotesigned", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPRemoteSigned;
            }
            else if (string.Equals(_executionPolicy, "bypass", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPBypass;
            }
            else if (string.Equals(_executionPolicy, "allsigned", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPAllSigned;
            }
            else if (string.Equals(_executionPolicy, "restricted", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPRestricted;
            }
            else if (string.Equals(_executionPolicy, "unrestricted", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPUnrestricted;
            }
            else if (string.Equals(_executionPolicy, "undefined", StringComparison.OrdinalIgnoreCase))
            {
                executionPolicySetting = ParameterBitmap.EPUndefined;
            }

            return executionPolicySetting;
        }

        private static bool MatchSwitch(string switchKey, string match, string smallestUnambiguousMatch)
        {
            Debug.Assert(!string.IsNullOrEmpty(match), "need a value");
            Debug.Assert(match.Trim().ToLowerInvariant() == match, "match should be normalized to lowercase w/ no outside whitespace");
            Debug.Assert(smallestUnambiguousMatch.Trim().ToLowerInvariant() == smallestUnambiguousMatch, "match should be normalized to lowercase w/ no outside whitespace");
            Debug.Assert(match.Contains(smallestUnambiguousMatch), "sUM should be a substring of match");

            return (switchKey.Length >= smallestUnambiguousMatch.Length
                    && match.StartsWith(switchKey, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        private void ShowError(PSHostUserInterface hostUI)
        {
            if (_error != null)
            {
                hostUI.WriteErrorLine(_error);
            }
        }

        private void ShowHelp(PSHostUserInterface hostUI, string? helpText)
        {
            if (helpText is null)
            {
                return;
            }

            if (_showHelp)
            {
                hostUI.WriteLine();
                hostUI.Write(helpText);
                if (_showExtendedHelp)
                {
                    hostUI.Write("See 'powershell /?' for usage instructions");
                }

                hostUI.WriteLine();
            }
        }

        private void DisplayBanner(PSHostUserInterface hostUI, string? bannerText)
        {
            if (_showBanner && !_showHelp)
            {
                // If banner text is not supplied do nothing.
                if (!string.IsNullOrEmpty(bannerText))
                {
                    hostUI.WriteLine(bannerText);
                }
            }
        }

        /// <summary>
        /// Processes all the command line parameters to ChaosDbgHost.  Returns the exit code to be used to terminate the process, or
        /// Success to indicate that the program should continue running.
        /// </summary>
        /// <param name="args">
        /// The command line parameters to be processed.
        /// </param>
        internal void Parse(string[] args)
        {
            if (_dirty)
            {
                throw new InvalidOperationException("This instance has already been used. Create a new instance.");
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                    throw new ArgumentNullException($"{nameof(args)}[{i}]", CommandLineParameterParserStrings.NullElementInArgs);
            }

            // Indicates that we've called this method on this instance, and that when it's done, the state variables
            // will reflect the parse.
            _dirty = true;

            ParseHelper(args);
        }

        private void ParseHelper(string[] args)
        {
            if (args.Length == 0)
            {
                return;
            }

            bool noexitSeen = false;

            for (int i = 0; i < args.Length; ++i)
            {
                (string switchKey, bool shouldBreak) switchKeyResults = GetSwitchKey(args, ref i, ref noexitSeen);
                if (switchKeyResults.shouldBreak)
                {
                    break;
                }

                string switchKey = switchKeyResults.switchKey;

                // If version is in the commandline, don't continue to look at any other parameters
                if (MatchSwitch(switchKey, "version", "v"))
                {
                    _showVersion = true;
                    _showBanner = false;
                    _noInteractive = true;
                    _skipUserInit = true;
                    _noExit = false;
                    ParametersUsed |= ParameterBitmap.Version;
                    break;
                }

                if (MatchSwitch(switchKey, "help", "h") || MatchSwitch(switchKey, "?", "?"))
                {
                    _showHelp = true;
                    _showExtendedHelp = true;
                    _abortStartup = true;
                    ParametersUsed |= ParameterBitmap.Help;
                }
                else if (MatchSwitch(switchKey, "login", "l"))
                {
                    // On Windows, '-Login' does nothing.
                    // On *nix, '-Login' is already handled much earlier to improve startup performance, so we do nothing here.
                    ParametersUsed |= ParameterBitmap.Login;
                }
                else if (MatchSwitch(switchKey, "noexit", "noe"))
                {
                    _noExit = true;
                    noexitSeen = true;
                    ParametersUsed |= ParameterBitmap.NoExit;
                }
                else if (MatchSwitch(switchKey, "noprofile", "nop"))
                {
                    _skipUserInit = true;
                    ParametersUsed |= ParameterBitmap.NoProfile;
                }
                else if (MatchSwitch(switchKey, "nologo", "nol"))
                {
                    _showBanner = false;
                    ParametersUsed |= ParameterBitmap.NoLogo;
                }
                else if (MatchSwitch(switchKey, "noninteractive", "noni"))
                {
                    _noInteractive = true;
                    ParametersUsed |= ParameterBitmap.NonInteractive;
                }
                else if (MatchSwitch(switchKey, "socketservermode", "so"))
                {
                    _socketServerMode = true;
                    _showBanner = false;
                    ParametersUsed |= ParameterBitmap.SocketServerMode;
                }
                else if (MatchSwitch(switchKey, "servermode", "s"))
                {
                    _serverMode = true;
                    _showBanner = false;
                    ParametersUsed |= ParameterBitmap.ServerMode;
                }
                else if (MatchSwitch(switchKey, "namedpipeservermode", "nam"))
                {
                    _namedPipeServerMode = true;
                    _showBanner = false;
                    ParametersUsed |= ParameterBitmap.NamedPipeServerMode;
                }
                else if (MatchSwitch(switchKey, "sshservermode", "sshs"))
                {
                    _sshServerMode = true;
                    _showBanner = false;
                    ParametersUsed |= ParameterBitmap.SSHServerMode;
                }
                else if (MatchSwitch(switchKey, "noprofileloadtime", "noprofileloadtime"))
                {
                    _noProfileLoadTime = true;
                    ParametersUsed |= ParameterBitmap.NoProfileLoadTime;
                }
                else if (MatchSwitch(switchKey, "interactive", "i"))
                {
                    _noInteractive = false;
                    ParametersUsed |= ParameterBitmap.Interactive;
                }
                else if (MatchSwitch(switchKey, "custompipename", "cus"))
                {
                    ++i;
                    if (i >= args.Length)
                    {
                        SetCommandLineError(
                            CommandLineParameterParserStrings.MissingCustomPipeNameArgument);
                        break;
                    }

#if UNIX
                    int maxNameLength = MaxNameLength();
                    if (args[i].Length > maxNameLength)
                    {
                        SetCommandLineError(
                            string.Format(
                                CommandLineParameterParserStrings.CustomPipeNameTooLong,
                                maxNameLength,
                                args[i],
                                args[i].Length));
                        break;
                    }
#endif

                    _customPipeName = args[i];
                    ParametersUsed |= ParameterBitmap.CustomPipeName;
                }
                else if (MatchSwitch(switchKey, "commandwithargs", "commandwithargs") || MatchSwitch(switchKey, "cwa", "cwa"))
                {
                    _commandHasArgs = true;

                    if (!ParseCommand(args, ref i, noexitSeen, false))
                    {
                        break;
                    }

                    i++;
                    CollectPSArgs(args, ref i);
                    ParametersUsed |= ParameterBitmap.CommandWithArgs;
                }
                else if (MatchSwitch(switchKey, "command", "c"))
                {
                    if (!ParseCommand(args, ref i, noexitSeen, false))
                    {
                        break;
                    }

                    ParametersUsed |= ParameterBitmap.Command;
                }
                else if (MatchSwitch(switchKey, "windowstyle", "w"))
                {
#if UNIX
                    SetCommandLineError(
                        CommandLineParameterParserStrings.WindowStyleArgumentNotImplemented);
                    break;
#else
                    ++i;
                    if (i >= args.Length)
                    {
                        SetCommandLineError(
                            CommandLineParameterParserStrings.MissingWindowStyleArgument);
                        break;
                    }

                    try
                    {
                        _windowStyle = LanguagePrimitives.ConvertTo<ProcessWindowStyle>(args[i]);
                    }
                    catch (PSInvalidCastException e)
                    {
                        SetCommandLineError(
                            string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.InvalidWindowStyleArgument, args[i], e.Message));
                        break;
                    }

                    ParametersUsed |= ParameterBitmap.WindowStyle;
#endif
                }
                else if (MatchSwitch(switchKey, "file", "f"))
                {
                    if (!ParseFile(args, ref i, noexitSeen))
                    {
                        break;
                    }

                    ParametersUsed |= ParameterBitmap.File;
                }
#if DEBUG
                else if (MatchSwitch(switchKey, "isswait", "isswait"))
                {
                    // Just toss this option, it was processed earlier in 'ManagedEntrance.Start()'.
                }
#endif
                else if (MatchSwitch(switchKey, "executionpolicy", "ex") || MatchSwitch(switchKey, "ep", "ep"))
                {
                    ParseExecutionPolicy(args, ref i, ref _executionPolicy, CommandLineParameterParserStrings.MissingExecutionPolicyParameter);
                    ParametersUsed |= ParameterBitmap.ExecutionPolicy;
                    var executionPolicy = GetExecutionPolicy(_executionPolicy);
                    if (executionPolicy == ParameterBitmap.EPIncorrect)
                    {
                        SetCommandLineError(
                            string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.InvalidExecutionPolicyArgument, _executionPolicy),
                            showHelp: true);
                        break;
                    }

                    ParametersUsed |= executionPolicy;
                }
                else if (MatchSwitch(switchKey, "encodedcommand", "e") || MatchSwitch(switchKey, "ec", "e"))
                {
                    _wasCommandEncoded = true;
                    if (!ParseCommand(args, ref i, noexitSeen, true))
                    {
                        break;
                    }

                    ParametersUsed |= ParameterBitmap.EncodedCommand;
                }
                else if (MatchSwitch(switchKey, "workingdirectory", "wo") || MatchSwitch(switchKey, "wd", "wd"))
                {
                    ++i;
                    if (i >= args.Length)
                    {
                        SetCommandLineError(
                            CommandLineParameterParserStrings.MissingWorkingDirectoryArgument);
                        break;
                    }

                    _workingDirectory = args[i];
                    ParametersUsed |= ParameterBitmap.WorkingDirectory;
                }
#if !UNIX
                else if (MatchSwitch(switchKey, "removeworkingdirectorytrailingcharacter", "removeworkingdirectorytrailingcharacter"))
                {
                    _removeWorkingDirectoryTrailingCharacter = true;
                }
#endif
                else
                {
                    // The first parameter we fail to recognize marks the beginning of the file string.
                    --i;
                    if (!ParseFile(args, ref i, noexitSeen))
                    {
                        break;
                    }

                    // default to filename being the next argument.
                    ParametersUsed |= ParameterBitmap.File;
                }
            }

            Debug.Assert(
                ((_exitCode == PSHostBase.ExitCodeBadCommandLineParameter) && _abortStartup)
                || (_exitCode == PSHostBase.ExitCodeSuccess),
                "if exit code is failure, then abortstartup should be true");
        }

        internal void ShowErrorHelpBanner(PSHostUserInterface hostUI, string? bannerText, string? helpText)
        {
            ShowError(hostUI);
            ShowHelp(hostUI, helpText);
            DisplayBanner(hostUI, bannerText);
        }

        private void SetCommandLineError(string msg, bool showHelp = false, bool showBanner = false)
        {
            if (_error != null)
            {
                throw new ArgumentException(nameof(SetCommandLineError), nameof(_error));
            }

            _error = msg;
            _showHelp = showHelp;
            _showBanner = showBanner;
            _abortStartup = true;
            _exitCode = PSHostBase.ExitCodeBadCommandLineParameter;
        }

        private void ParseExecutionPolicy(string[] args, ref int i, ref string? executionPolicy, string resourceStr)
        {
            ++i;
            if (i >= args.Length)
            {
                SetCommandLineError(resourceStr, showHelp: true);
                return;
            }

            executionPolicy = args[i];
        }

        // Process file execution. We don't need to worry about checking -command
        // since if -command comes before -file, -file will be treated as part
        // of the script to evaluate. If -file comes before -command, it will
        // treat -command as an argument to the script...
        private bool ParseFile(string[] args, ref int i, bool noexitSeen)
        {
            ++i;
            if (i >= args.Length)
            {
                SetCommandLineError(
                    CommandLineParameterParserStrings.MissingFileArgument,
                    showHelp: true,
                    showBanner: false);
                return false;
            }

            // Don't show the startup banner unless -noexit has been specified.
            if (!noexitSeen)
                _showBanner = false;

            // Process interactive input...
            if (args[i] == "-")
            {
                // the arg to -file is -, which is secret code for "read the commands from stdin with prompts"

                _explicitReadCommandsFromStdin = true;
                _noPrompt = false;
            }
            else
            {
                // Exit on script completion unless -noexit was specified...
                if (!noexitSeen)
                    _noExit = false;

                // We need to get the full path to the script because it will be
                // executed after the profiles are run and they may change the current
                // directory.
                try
                {
                    _file = NormalizeFilePath(args[i]);
                }
                catch (Exception e)
                {
                    // Catch all exceptions - we're just going to exit anyway so there's
                    // no issue of the system being destabilized.
                    SetCommandLineError(
                        string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.InvalidFileArgument, args[i], e.Message),
                        showBanner: false);
                    return false;
                }

                if (!System.IO.File.Exists(_file))
                {
                    if (args[i].StartsWith("-") && args[i].Length > 1)
                    {
                        string param = args[i].Substring(1, args[i].Length - 1);
                        StringBuilder possibleParameters = new StringBuilder();
                        foreach (string validParameter in s_validParameters)
                        {
                            if (validParameter.IndexOf(param, StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                possibleParameters.Append("\n  -");
                                possibleParameters.Append(validParameter);
                            }
                        }

                        if (possibleParameters.Length > 0)
                        {
                            SetCommandLineError(
                                string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.InvalidArgument, args[i])
                                + Environment.NewLine
                                + possibleParameters.ToString(),
                                showBanner: false);
                            return false;
                        }
                    }

                    SetCommandLineError(
                        string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.ArgumentFileDoesNotExist, args[i]),
                        showHelp: true);
                    return false;
                }
#if !UNIX
                // Only do the .ps1 extension check on Windows since shebang is not supported
                if (!_file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    SetCommandLineError(string.Format(CultureInfo.CurrentCulture, CommandLineParameterParserStrings.InvalidFileArgumentExtension, args[i]));
                    return false;
                }
#endif

                i++;

                CollectPSArgs(args, ref i);
            }

            return true;
        }

        private void CollectPSArgs(string[] args, ref int i)
        {
            // Try parse '$true', 'true', '$false' and 'false' values.
            static object ConvertToBoolIfPossible(string arg)
            {
                // Before parsing we skip '$' if present.
                return arg.Length > 0 && bool.TryParse(arg.Substring(arg[0] == '$' ? 1 : 0), out bool boolValue)
                    ? (object)boolValue
                    : (object)arg;
            }

            string? pendingParameter = null;

            while (i < args.Length)
            {
                string arg = args[i];

                // If there was a pending parameter, add a named parameter
                // using the pending parameter and current argument
                if (pendingParameter != null)
                {
                    _collectedArgs.Add(new CommandParameter(pendingParameter, arg));
                    pendingParameter = null;
                }
                else if (!string.IsNullOrEmpty(arg) && arg[0] == '-' && arg.Length > 1)
                {
                    int offset = arg.IndexOf(':');
                    if (offset >= 0)
                    {
                        if (offset == arg.Length - 1)
                        {
                            pendingParameter = arg.TrimEnd(':');
                        }
                        else
                        {
                            string argValue = arg.Substring(offset + 1);
                            string argName = arg.Substring(0, offset);
                            _collectedArgs.Add(new CommandParameter(argName, ConvertToBoolIfPossible(argValue)));
                        }
                    }
                    else
                    {
                        _collectedArgs.Add(new CommandParameter(arg));
                    }
                }
                else
                {
                    _collectedArgs.Add(new CommandParameter(null, arg));
                }

                ++i;
            }
        }

        private bool ParseCommand(string[] args, ref int i, bool noexitSeen, bool isEncoded)
        {
            if (_commandLineCommand != null)
            {
                // we've already set the command, so squawk
                SetCommandLineError(CommandLineParameterParserStrings.CommandAlreadySpecified, showHelp: true);
                return false;
            }

            ++i;
            if (i >= args.Length)
            {
                SetCommandLineError(CommandLineParameterParserStrings.MissingCommandParameter, showHelp: true);
                return false;
            }

            if (isEncoded)
            {
                try
                {
                    _commandLineCommand = new string(Encoding.Unicode.GetChars(Convert.FromBase64String(args[i])));
                }
                // decoding failed
                catch
                {
                    SetCommandLineError(CommandLineParameterParserStrings.BadCommandValue, showHelp: true);
                    return false;
                }
            }
            else if (args[i] == "-")
            {
                // the arg to -command is -, which is secret code for "read the commands from stdin with no prompts"

                _explicitReadCommandsFromStdin = true;
                _noPrompt = true;

                ++i;
                if (i != args.Length)
                {
                    // there are more parameters to -command than -, which is an error.

                    SetCommandLineError(CommandLineParameterParserStrings.TooManyParametersToCommand, showHelp: true);
                    return false;
                }

                if (InputRedirectedTestHook.HasValue ? !InputRedirectedTestHook.Value : !Console.IsInputRedirected)
                {
                    SetCommandLineError(CommandLineParameterParserStrings.StdinNotRedirected, showHelp: true);
                    return false;
                }
            }
            else
            {
                if (_commandHasArgs)
                {
                    _commandLineCommand = args[i];
                }
                else
                {
                    _commandLineCommand = string.Join(" ", args, i, args.Length - i);
                    i = args.Length;
                }
            }

            if (!noexitSeen && !_explicitReadCommandsFromStdin)
            {
                // don't reset this if they've already specified -noexit
                _noExit = false;
            }

            _showBanner = false;

            return true;
        }
    }
}
