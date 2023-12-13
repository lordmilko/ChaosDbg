using System;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.DAC;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Encapsulates a <see cref="ClrDebug.CorDebugProcess"/> being debugged by a <see cref="CordbEngine"/>.
    /// </summary>
    public class CordbProcess : IDisposable
    {
        #region Overview

        //High level information about the target process

        /// <summary>
        /// Gets the ID of this process.<para/>
        /// This property is always safe to access, regardless of the state of the underlying process.
        /// </summary>
        public int Id => CorDebugProcess.Id;

        /// <summary>
        /// Gets the handle of the process.
        /// </summary>
        public IntPtr Handle => CorDebugProcess.Handle;

        /// <summary>
        /// Gets whether the target is a 32-bit process.
        /// </summary>
        public bool Is32Bit { get; }

        /// <summary>
        /// Gets the <see cref="IMAGE_FILE_MACHINE"/> type of this process.
        /// </summary>
        public IMAGE_FILE_MACHINE MachineType => Is32Bit ? IMAGE_FILE_MACHINE.I386 : IMAGE_FILE_MACHINE.AMD64;

        /// <summary>
        /// Gets the command line that was used to launch the process.
        /// </summary>
        public string CommandLine { get; }

        #endregion
        #region Related Entities

        //Objects that store additionl information about the process

        /// <summary>
        /// Gets the underlying <see cref="ClrDebug.CorDebugProcess"/> of this entity.
        /// </summary>
        public CorDebugProcess CorDebugProcess { get; }

        /// <summary>
        /// Gets the container containing the threads that have been loaded into the current process.
        /// </summary>
        public CordbThreadStore Threads { get; }

        /// <summary>
        /// Gets the container containing the modules that have been loaded into the current process.
        /// </summary>
        public CordbModuleStore Modules { get; }

        #endregion
        #region Services

        //Objects that provide access to additional services for interacting with the process

        /// <summary>
        /// Provides access to information about the process that comes form the DAC.
        /// </summary>
        public DacProvider DAC { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Lazy<DbgHelpSession> dbgHelp;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public DbgHelpSession DbgHelp => dbgHelp.Value;

        #endregion
        #region Debugger State

        //Values that provide information about the state and the way in which the process is being debugged

        public CordbSessionInfo Session { get; }

        /// <summary>
        /// Gets whether any thread currently has queued callbacks in need of dispatching.<para/>
        /// When attempting to stop the debugger, all queued callbacks must have been dispatched in order to ascertain
        /// the true state of the debugger. Failure to do so will result in CORDBG_E_PROCESS_NOT_SYNCHRONIZED upon
        /// trying to introspect the process. See also: https://learn.microsoft.com/en-us/archive/blogs/jmstall/using-icordebugprocesshasqueuedcallbacks
        /// </summary>
        public bool HasQueuedCallbacks => CorDebugProcess.HasQueuedCallbacks(null);

        #endregion

        private bool disposed;

        public CordbProcess(
            CorDebugProcess corDebugProcess,
            CordbSessionInfo session,
            CordbEngineServices services,
            bool is32Bit,
            string commandLine)
        {
            if (corDebugProcess == null)
                throw new ArgumentNullException(nameof(corDebugProcess));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (services == null)
                throw new ArgumentNullException(nameof(services));

            CorDebugProcess = corDebugProcess;
            Session = session;
            Is32Bit = is32Bit;
            CommandLine = commandLine;

            Threads = new CordbThreadStore(this);
            Modules = new CordbModuleStore(this, services);

            DAC = new DacProvider(this);

            //The handle is not safe to retrieve if the CorDebugProcess has already been neutered, so we'll see if this causes an issue when calling SymCleanup() or not
            dbgHelp = new Lazy<DbgHelpSession>(() => new DbgHelpSession(corDebugProcess.Handle));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (dbgHelp.IsValueCreated)
                dbgHelp.Value.Dispose();

            disposed = true;
        }
    }
}
