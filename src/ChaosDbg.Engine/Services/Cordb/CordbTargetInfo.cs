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
        /// Gets or sets the current status of the target.
        /// </summary>
        public EngineStatus Status { get; set; }

        public CordbProcess Process { get; }

        /// <summary>
        /// Gets or sets the last thread that was seen by a debug event.
        /// </summary>
        public CorDebugThread ActiveThread { get; set; }

        public CordbTargetInfo(string commandLine, CorDebugProcess process, bool is32Bit, bool isInterop)
        {
            CommandLine = commandLine;
            Process = new CordbProcess(process, is32Bit, isInterop);
        }
    }
}
