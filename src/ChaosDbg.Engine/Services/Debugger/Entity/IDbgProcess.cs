using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a process that is being debugged.<para/>
    /// Concrete implementations include <see cref="CordbProcess"/> and <see cref="DbgEngProcess"/>.
    /// </summary>
    public interface IDbgProcess
    {
        #region Overview

        /// <summary>
        /// Gets the process ID of this process.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets whether the target is a 32-bit process.
        /// </summary>
        public bool Is32Bit { get; }

        /// <summary>
        /// Gets the command line that was used to launch the process.
        /// </summary>
        public string[] CommandLine { get; }

        #endregion
        #region Stores / Related Entities

        /// <summary>
        /// Gets the container containing the threads that have been loaded into the current process.
        /// </summary>
        IDbgThreadStore Threads { get; }

        /// <summary>
        /// Gets the container containing the modules that have been loaded into the current process.
        /// </summary>
        IDbgModuleStore Modules { get; }

        #endregion
    }
}
