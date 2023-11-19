using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Stores information about a debug target.
    /// </summary>
    public class DbgEngTargetInfo
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
        /// Gets or sets the current status of the target.
        /// </summary>
        public DEBUG_STATUS Status { get; set; }

        /// <summary>
        /// Gets whether the target is a 32-bit process.
        /// </summary>
        public bool Is32Bit { get; }

        public DbgEngTargetInfo(string commandLine, int processId, bool is32Bit)
        {
            CommandLine = commandLine;
            ProcessId = processId;
            Is32Bit = is32Bit;
        }
    }
}
