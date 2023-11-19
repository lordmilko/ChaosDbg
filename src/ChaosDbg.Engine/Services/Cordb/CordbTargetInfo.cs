using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Stores information about a debug target.
    /// </summary>
    public class CordbTargetInfo
    {
        /// <summary>
        /// Gets the command line that was used to launch the debug target.
        /// </summary>
        public string CommandLine { get; }

        /// <summary>
        /// Gets the process ID of the debug target.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the <see cref="CorDebugProcess"/> that is being debugged.
        /// </summary>
        public CorDebugProcess Process { get; }

        /// <summary>
        /// Gets or sets the last thread that was seen by a debug event.
        /// </summary>
        public CorDebugThread ActiveThread { get; set; }

        /// <summary>
        /// Gets whether the target is a 32-bit process.
        /// </summary>
        public bool Is32Bit { get; }

        public CordbTargetInfo(string commandLine, int processId, CorDebugProcess process, bool is32Bit)
        {
            CommandLine = commandLine;
            ProcessId = processId;
            Process = process;
            Is32Bit = is32Bit;
        }
    }
}
