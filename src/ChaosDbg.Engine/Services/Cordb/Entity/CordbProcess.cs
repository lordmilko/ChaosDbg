using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ChaosDbg.DAC;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /* There are several entry points into the ICorDebug API. Depending on the method that is used, this can affect
     * the capabilities the ICorDebug API provides to you.
     *
     * ICorDebug's source code makes several references to product codenames and engine versions. These references
     * are as follows:
     *
     * Everett (V1.1): Visual Studio .NET 2003 (.NET Framework 1.1)
     * Whidbey (V2): Visual Studio 2005 (.NET Framework 2.0)
     * Orcas (V3): Visual Studio 2008 (.NET Framework 3.5)
     * Arrowhead: .NET Framework 3.5 SP1
     *
     * In the original implementation of mscordbi (V1.0/V1.1), CorDebug was a CoClass. In V2+ you must use one of the methods
     * exported from mscordbi to create an ICorDebug instance of a specific version. Nowadays there are really only two APIs
     * you can use in mscordbi: V2 and V3. The V2 API is also known as the "in-process" API. In the V2 API, there are two "sides"
     * to the debugger: the "left side" (the Debugger class, whose global instance lives in (debugger.cpp!g_pDebugger)) and the
     * "right side" (mscordbi). When a process is created or attached using the V2 API, two things happen: first, a ShimProcess is
     * created inside the CordbProcess that creates a CordbWin32EventThread used for pumping debugger events. Then, the in-process
     * Debugger class is notified that we're debugging it via the CordbProcess::QueueManagedAttachIfNeededWorker method.
     * This method is called in one of two places. Upon processing a native debug event, ShimProcess::HandleWin32DebugEvent will either:
     *
     * - call CordbProcess::HandleDebugEventForInteropDebugging which, after dispatching our DebugEvent callback, will call
     *   CordbWin32EventThread::UnmanagedContinue -> CordbWin32EventThread::DoDbgContinue -> queue the pending attach
     *
     * - call ShimProcess::DefaultEventHandler -> queue the pending attach
     *
     * The V3 API by contrast is considered an "out-of-process" architecture. It enables debugging targets that aren't necessarily
     * running, such as memory dumps. As a result, in V3 the in-process Debugger has no clue that you're debugging it, which means
     * you won't receive managed callbacks from the "right side", and and there also won't be an event thread running inside ICorDebug
     * pumping debug events. The V3 API is useful therefore when you've got your own debug engine and simply want to perform introspection
     * against managed entities. This is what SOS does to do certain things that you can't do with SOSDacInterface. Certain methods
     * such as ICorDebugProcess::GetID() and ICorDebugThread::GetHandle() are also unsupported in V3, due to the fact there might not
     * be a live process to grab this information for.
     *
     * The following shows how ICorDebug is created in .NET Framework vs .NET Core
     *   mscoreei!CLRRuntimeInfoImpl::GetInterface    -> mscoreei!GetLegacyDebuggingInterfaceInternal -> mscordbi!CreateCordbObject
     *   dbgshim!CreateDebuggingInterfaceFromVersion* -> dbgshim!CreateCoreDbg                        -> mscordbi!CoreCLRCreateCordbObject
     *
     * The following function shows the ways in which a mscordbi!CordbProcess gets created using the V2
     * pipeline containing a ShimProcess
     *
     *   Cordb::DebugActiveProcess -> Cordb::DebugActiveProcessCommon -> ShimProcess::DebugActiveProcess
     *   Cordb::CreateProcess      -> Cordb::CreateProcessCommon      -> ShimProcess::CreateProcess
     *
     * The following shows the ways in which a mscordbi!CordbProcess gets created using the V2 pipeline
     * without a ShimProcess. SOS uses this code path to get an ICorDebugProcess
     *   OpenVirtualProcessImpl2   -> OpenVirtualProcessImpl (as below)
     *   OpenVirtualProcessImpl    -> CordbProcess::OpenVirtualProcess
     * */

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

        public DbgHelpSession DbgHelp { get; }

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
            DbgHelp = new DbgHelpSession(corDebugProcess.Handle, invadeProcess: false);
        }

        /// <summary>
        /// Initiates a request that the target process be terminated, and waits for the <see cref="CorDebugManagedCallbackKind.ExitProcess"/> event to be emitted.
        /// </summary>
        internal void Terminate()
        {
            Debug.Assert(Session.WaitExitProcess == null);

            Session.WaitExitProcess = new TaskCompletionSource<object>();

            CorDebugProcess.Terminate(0);

            Session.WaitExitProcess.Task.Wait();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DbgHelp.Dispose();

            disposed = true;
        }
    }
}
