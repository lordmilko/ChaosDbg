using System;
using System.Diagnostics;
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
        /// <summary>
        /// Gets the underlying <see cref="ClrDebug.CorDebugProcess"/> of this entity.
        /// </summary>
        public CorDebugProcess CorDebugProcess { get; }

        /// <summary>
        /// Gets the ID of this process.<para/>
        /// This property is always safe to access, regardless of the state of the underlying process.
        /// </summary>
        public int Id => CorDebugProcess.Id;

        public IntPtr Handle => CorDebugProcess.Handle;

        /// <summary>
        /// Gets whether the target is a 32-bit process.
        /// </summary>
        public bool Is32Bit { get; }

        /// <summary>
        /// Gets whether both managed and native code are being debugged in the process.
        /// </summary>
        public bool IsInterop { get; }

        public IMAGE_FILE_MACHINE MachineType => Is32Bit ? IMAGE_FILE_MACHINE.I386 : IMAGE_FILE_MACHINE.AMD64;

        /// <summary>
        /// Provides access to information about the process that comes form the DAC.
        /// </summary>
        public DacProvider DAC { get; }

        /// <summary>
        /// Gets whether any thread currently has queued callbacks in need of dispatching.<para/>
        /// When attempting to stop the debugger, all queued callbacks must have been dispatched in order to ascertain
        /// the true state of the debugger. Failure to do so will result in CORDBG_E_PROCESS_NOT_SYNCHRONIZED upon
        /// trying to introspect the process. See also: https://learn.microsoft.com/en-us/archive/blogs/jmstall/using-icordebugprocesshasqueuedcallbacks
        /// </summary>
        public bool HasQueuedCallbacks => CorDebugProcess.HasQueuedCallbacks(null);

        /// <summary>
        /// Gets the container containing the threads that have been loaded into the current process.
        /// </summary>
        public CordbThreadStore Threads { get; }

        /// <summary>
        /// Gets the container containing the modules that have been loaded into the current process.
        /// </summary>
        public CordbModuleStore Modules { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Lazy<DbgHelpSession> dbgHelp;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public DbgHelpSession DbgHelp => dbgHelp.Value;

        private bool disposed;

        public CordbProcess(CorDebugProcess corDebugProcess, bool is32Bit, bool isInterop)
        {
            if (corDebugProcess == null)
                throw new ArgumentNullException(nameof(corDebugProcess));

            CorDebugProcess = corDebugProcess;
            Is32Bit = is32Bit;
            IsInterop = isInterop;

            Threads = new CordbThreadStore(this);
            Modules = new CordbModuleStore(this);

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
