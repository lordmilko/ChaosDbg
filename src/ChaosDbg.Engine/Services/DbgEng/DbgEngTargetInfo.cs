using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Stores information about a debug target.
    /// </summary>
    public class DbgEngTargetInfo
    {
        /// <summary>
        /// Gets the process name of the debug target.
        /// </summary>
        public string ProcessName { get; }

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

        public DbgEngTargetInfo(string processName, int processId, bool is32Bit)
        {
            ProcessName = processName;
            ProcessId = processId;
            Is32Bit = is32Bit;
        }
    }
}
