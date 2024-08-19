using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosDbg.DAC;
using ChaosDbg.Disasm;
using ChaosDbg.Evaluator.Masm;
using ChaosDbg.Symbol;
using ChaosLib;
using ChaosLib.Handle;
using ChaosLib.Symbols;
using ChaosLib.TypedData;
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
     *   CordbWin32EventThread::UnmanagedContinue -> CordbWin32EventThread::DoDbgContinue -> which will call QueueManagedAttachIfNeededWorker
     *   after the loader breakpoint is received.
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
    public class CordbProcess : IDbgProcess, ICLRDebuggingLibraryProvider, IDisposable
    {
        #region Overview

        //High level information about the target process

        /// <inheritdoc />
        public int Id { get; }

        /// <summary>
        /// Gets the handle of the process.
        /// </summary>
        public IntPtr Handle { get; }

        /// <inheritdoc />
        public bool Is32Bit { get; }

        /// <summary>
        /// Gets whether this process encapsulates a V3 <see cref="CorDebugProcess"/>, indicating there is no managed callback and that the process is always "live".
        /// </summary>
        public bool IsV3 { get; }

        /// <summary>
        /// Gets the <see cref="IMAGE_FILE_MACHINE"/> type of this process.
        /// </summary>
        public IMAGE_FILE_MACHINE MachineType => Is32Bit ? IMAGE_FILE_MACHINE.I386 : IMAGE_FILE_MACHINE.AMD64;

        /// <inheritdoc />
        public string[] CommandLine { get; }

        #endregion
        #region Stores / Related Entities

        //Objects that store additionl information about the process

        /// <summary>
        /// Gets the underlying <see cref="ClrDebug.CorDebugProcess"/> of this entity.
        /// </summary>
        public CorDebugProcess CorDebugProcess { get; }

#if DEBUG
        /// <summary>
        /// Provides access to the native mscordbi!CordbProcess via typed data.
        /// </summary>
        internal IDbgRemoteObject TypedProcess { get; }
#endif

        public Process Win32Process { get; }

        /// <inheritdoc cref="IDbgProcess.Threads" />
        public CordbThreadStore Threads { get; }

        public CordbAppDomainStore AppDomains { get; }

        public CordbAssemblyStore Assemblies { get; }

        /// <inheritdoc cref="IDbgProcess.Modules" />
        public CordbModuleStore Modules { get; }

        public CordbBreakpointStore Breakpoints { get; }

        #endregion
        #region Services

        //Objects that provide access to additional services for interacting with the process

        /// <summary>
        /// Provides access to information about the process that comes form the DAC.
        /// </summary>
        public DacProvider DAC { get; }

        /// <summary>
        /// Gets a data target that is used to communicate with the process.<para/>
        /// The advantage of using CorDebugProcess.ReadMemory is that any breakpoints that have been set on the right side
        /// will be made transparent to the caller. The downside is that this can fail when executed on the Win32 Event Thread.
        /// Using a custom data target bypasses issues you may encounter on the Win32 Event Thread, but any patches defined won't
        /// be hidden from you.
        /// </summary>
        public ICLRDataTarget DataTarget => DAC.DataTarget;

        public MasmEvaluator Evaluator { get; }

        /// <summary>
        /// Provides access to the symbols contained within this process.
        /// </summary>
        public DebuggerSymbolProvider Symbols { get; }

        /// <summary>
        /// Gets a disassembler capable of disassembling any instruction in this process.
        /// </summary>
        public INativeDisassembler ProcessDisassembler { get; }

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

        #region V2

        /// <summary>
        /// Initializes a new instance of the <see cref="CordbProcess"/> class for a V2 debugger pipeline.
        /// </summary>
        /// <param name="corDebugProcess">The <see cref="CorDebugProcess"/> to encapsulate.</param>
        /// <param name="session">The debugger session state that is associated with this process.</param>
        /// <param name="is32Bit">Whether the target process is 32-bit.</param>
        /// <param name="commandLine">If the operating system process was newly created, the command line arguments that were used to launch the process.</param>
        public CordbProcess(
            CorDebugProcess corDebugProcess,
            CordbSessionInfo session,
            bool is32Bit,
            string commandLine) : this(corDebugProcess.Id, corDebugProcess.Handle, is32Bit, false)
        {
            if (corDebugProcess == null)
                throw new ArgumentNullException(nameof(corDebugProcess));

            CorDebugProcess = corDebugProcess;

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            Session = session;

            if (commandLine != null)
                CommandLine = Shell32.CommandLineToArgvW(commandLine);

            DAC = new DacProvider(this);

            //DbgHelp.Native.SymSetOptions(ClrDebug.DbgEng.SYMOPT.DEFERRED_LOADS); //temp

            //The handle is not safe to retrieve if the CorDebugProcess has already been neutered, so we'll see if this causes an issue when calling SymCleanup() or not
            Symbols = new DebuggerSymbolProvider(
                corDebugProcess.Handle,
                Session.EngineId,
                new CordbDebuggerSymbolProviderExtension(this),
                Session.Services.DbgHelpProvider,
                Session.Services.MicrosoftPdbSourceFileProvider
            );

            using var dbgHelpHolder = new DisposeHolder(Symbols);

            var resolver = new CordbDisasmSymbolResolver(this);
            ProcessDisassembler = session.Services.NativeDisasmProvider.CreateDisassembler(this, resolver);
            resolver.ProcessDisassembler = ProcessDisassembler;

#if DEBUG
            TypedProcess = CorDebugProcess.Raw.GetType().IsCOMObject
                ? NativeReflector.GetTypedData(CorDebugProcess)
                : null;
#endif

            //This MUST be last, no exceptions!
            dbgHelpHolder.SuppressDispose();
        }

        #endregion
        #region V3

        public unsafe CordbProcess(Process process) : this(process.Id, process.Handle, Kernel32.IsWow64Process(process.Handle), true)
        {
            DAC = new DacProvider(this);

            //Force load mscordacwks
            _ = DAC.SOS;

            //Currently only .NET Framework is supported
            var clrDebugging = Extensions.CLRCreateInstance().CLRDebugging;

            var maxDebuggerSupportedVersion = new CLR_DEBUGGING_VERSION { wMajor = 4 };
            var version = new CLR_DEBUGGING_VERSION();

            var result = clrDebugging.OpenVirtualProcess(
                (long) (void*) Kernel32.GetModuleHandleW("clr.dll"),
                DAC.DataTarget,
                this,
                maxDebuggerSupportedVersion,
                typeof(ICorDebugProcess).GUID,
                ref version
            );

            CorDebugProcess = new CorDebugProcess((ICorDebugProcess) result.ppProcess);

#if DEBUG
            TypedProcess = NativeReflector.GetTypedData(CorDebugProcess);
#endif
        }

        HRESULT ICLRDebuggingLibraryProvider.ProvideLibrary(string pwszFileName, int dwTimestamp, int dwSizeOfImage, out IntPtr phModule)
        {
            if (Kernel32.TryGetModuleHandleW(pwszFileName, out phModule) == HRESULT.S_OK)
                return HRESULT.S_OK;

            //We expect that mscordacwks should be loaded, but mscordbi might not yet be
            var dbiPath = Path.Combine(Path.GetDirectoryName(DAC.CLR.FileName), pwszFileName);

            phModule = Kernel32.LoadLibrary(dbiPath);
            return HRESULT.S_OK;
        }

        #endregion
        #region V2 / V3

        private CordbProcess(
            int processId,
            IntPtr hProcess,
            bool is32Bit,
            bool isV3)
        {
            IsV3 = isV3;

            //When interop debugging, you often can't request this from the Win32EventThread (most likely due to marshalling issues), and
            //when using the V3 API you can't request this property at all
            Id = processId;

            //Can't request this from the CorDebugProcess in V3
            Handle = hProcess;

            Win32Process = Process.GetProcessById(processId);
            
            Is32Bit = is32Bit;

            Threads = new CordbThreadStore(this);
            AppDomains = new CordbAppDomainStore(this);
            Assemblies = new CordbAssemblyStore(this);
            Modules = new CordbModuleStore(this);
            Breakpoints = new CordbBreakpointStore(this);

            Evaluator = new MasmEvaluator(new CordbMasmEvaluatorContext(this));
        }

        #endregion
        #region ReadManagedMemory

        //Prefer CorDebugProcess.ReadMemory so that any internal breakpoints are hidden, falling back to an ICLRDataTarget as required
        //(i.e. we're on the Win32 Event Thread and CorDebugProcess.ReadMemory won't work)

        public byte[] ReadManagedMemory(CORDB_ADDRESS address, int size)
        {
            var hr = CorDebugProcess.TryReadMemory(address, size, out var result);

            if (hr == HRESULT.S_OK)
                return result;

            //If we're stopped at an unsafe point, try fallback to using our data target instead
            if (hr == HRESULT.CORDBG_E_PROCESS_NOT_SYNCHRONIZED)
                return DataTarget.ReadVirtual(address, size);

            throw new DebugException(hr);
        }

        #endregion

        /// <summary>
        /// Initiates a request that the target process be terminated, and waits for the <see cref="CorDebugManagedCallbackKind.ExitProcess"/> event to be emitted.
        /// </summary>
        [Obsolete("Do not call this method directly. Call CordbEngine.Terminate() instead")]
        internal void Terminate()
        {
            //See the notes under the CordbEngine.cs: Shutdown section for information on how ICorDebug shutdown logic works, and how ChaosDbg
            //has been designed to interoperate with that.

            /* It is absolutely essential that we dispose our threads in order for the process to properly terminate. If any single process in the system is holding a reference to a given thread handle,
             * the process won't be able to terminate, as the handle will be being kept alive. If the process doesn't terminate, mscordbi won't know to trigger an ExitProcess
             * notification event. Having said this, it's important to note that every handle that was given out by CREATE_THREAD_DEBUG_EVENT is owned by Kernel32.
             * These handles will be closed by Kernel32 after the EXIT_PROCESS_DEBUG_EVENT has been processed. So it's expected there will at least be _some_ outstanding
             * thread handles after we call this. It could also be the case that we don't own any thread handles and this method call will do nothing.
             *
             * Ordinarily, we won't have any handles to dispose, but in in V3, our ManagedAccessors open handles. */
            Threads.Dispose();

            //As soon as we call terminate it's going to dispatch to the Win32 event thread an event requesting termination; thus, we want to ensure
            //we have our bookkeeping done
tryTerminate:
            Session.IsTerminating = true;
            var hr = CorDebugProcess.TryTerminate(0);

            Log.Debug<CordbProcess>("CorDebugProcess.Terminate() returned with {hr}", hr);

            switch (hr)
            {
                //If we're requesting termination at the same time that ICorDebug notices that the process has now terminated,
                //it will terminate and neuter the object, and we'll get this response back
                case HRESULT.CORDBG_E_PROCESS_TERMINATED:
                case HRESULT.CORDBG_E_OBJECT_NEUTERED:
                    //It's not our responsibility to claim that the process has terminated; only the ExitProcess event can do that
                    break;

                case HRESULT.CORDBG_E_PROCESS_NOT_SYNCHRONIZED:
                    //We currently make the assumption that the debugger thread inside of the target won't be interrupted
                    Log.Debug<CordbProcess>("Failed to terminate process as process was not synchronized. Attempting to stop process");
                    hr = CorDebugProcess.TryStop(0);

                    //I've seen a case where CordbEngine.Continue() was called after we called Stop() (not sure if it was our unit test that called Continue()
                    //or it occurred within a currently executing callback). In any case, if someone starts the debugger again, we'll just stop it again.
                    //There is a risk that we could loop infinitely here however though; I'm betting on getting a different error besides not synchronized
                    //if we keep trying this long enough though
                    Log.Debug<CordbProcess>("CorDebugProcess.Stop() returned with {hr}. Reattempting termination", hr);
                    goto tryTerminate;

                default:
                    hr.ThrowOnNotOK();
                    break;
            }

            /* We need to ensure the debugger is running in order to receive the exit process event.
             * If the process has already terminated, I believe we still need to wait for the ExitProcess event anyway; if we let this method
             * return without having received it, we may end up receiving it after we've already set our Process object to null, and we assert
             * that we must always have a Process object in our pre-event handler
             *
             * Ostensibly, the stop count on mscordbi!CordbProcess should match the stop count on our CordbSessionInfo. However, if an unhandled
             * exception occurs during the handling of a managed callback, this will not be the case. mscordbi!CordbProcess::DispatchRCEvent
             * bumps up the m_stopCount _twice_ so that it guarantees that the target is still stopped even after you call Continue(). We don't
             * know whether we're in this scenario, so to be safe, we'll just keep on calling continue until we're told to stop */

            if (Session.EventHistory.LastStopReason is CordbNativeEventPauseReason {OutOfBand: true})
                CorDebugProcess.TryContinue(true);

            //Keep calling continue until until we're told to stop
            while (true)
            {
                //Note that if this is the Win32 Event Thread, we're in big trouble, because when we send a non-OOB unmanaged continue,
                //the Win32 Event Thread needs to process it, but that's not going to be possible because we're on it, and we need
                //to block waiting for our WaitExitProcess event
                if (CorDebugProcess.TryContinue(false) != HRESULT.S_OK)
                    break;
            }

            //Target is now running. No point checking m_stopCount, as it may just be showing us what's going on on the Win32 Event Thread

            /* We are waiting for mscordbi to call CordbWin32EventThread::ExitProcess. Terminate() above literally attempts to terminate the process. ExitProcess is then called under the following scenarios:
             *
             * In CordbWin32EventThread::Win32EventLoop():
             * - the wait handle for the process indicates it has exited
             * - a W32ETA_DETACH event is sent to the Win32 Event Thread
             *
             * In CordbWin32EventThread::DoDbgContinue when a EXIT_PROCESS_DEBUG_EVENT event is received
             *
             * When is DoDbgContinue called?
             *
             * - in CordbProcess::DispatchUnmanagedInBandEvent() after dispatching an in-band event
             * - in CordbProcess::DispatchUnmanagedOOBEvent() when m_dispatchingOOBEvent is false (which should be the case if we continued the event)
             * - in CordbProcess::HandleDebugEventForInteropDebugging
             * - in CordbWin32EventThread::UnmanagedContinue
             */
#if DEBUG
            //Process.HasExited lies to you! You can have a process still alive even though it says its exited if there are still outstanding handles
            //to any of the processes' threads

tryWait:
            //With all the logging that goes on, it's not completely impossible that there could be a >30s delay for shutdown to even get to the point that we get the exit process event.
            //When running unit tests with the inproc DbgHelp, there can also be massive amounts of contention which causes issues when we've received the unmanaged ExitProcess notification event,
            //but are now hung waiting to enter DbgHelp in order to unload the unmanaged module for our process, creating the impression that we've hung when we haven't
            if (!Session.WaitExitProcess.Wait(40000))
            {
                if (Session.IsPerformingExitProcess)
                    goto tryWait;

                ValidateReadyToTerminate();

                //If we've received the EXIT_PROCESS_DEBUG_EVENT, but m_dispatchingOOBEvent is true, this either means
                //- that we're in the middle of EXIT_PROCESS_DEBUG_EVENT and it's hung, or
                //- our eager calls to Continue() above have stuffed up the real continue for an OOB event, and m_dispatchingOOBEvent was never set to false, preventing DoDbgContinue from being called
                Debug.Assert(false, "Hung while waiting for process to exit");
            }
#endif

            Session.WaitExitProcess.Wait();
        }

        private void ValidateReadyToTerminate()
        {
            /* If any outstanding handles are held to any threads in the target process, it won't terminate properly, and will exist in a zombie state.
             * Attempts to query it by PID will still succeed, Process.HasExited will return true despite the fact the process is still alive, and
             * perhaps most importantly: we won't receive our ExitProcess event, because the final thread of the process hasn't actually exited yet.
             * Thus, rather than just try and proceed with termination and wonder what is going on when we end up hanging waiting for WaitExitProcess
             * to be signalled, we'll preemptively try and query for any handles to the target process that might exist. While in theory we should
             * be scanning all handles in all processes (since any one of them could theoretically have a handle that may be keeping our target process
             * open), for performance and stability purposes (we currently may throw if we fail to retrieve handle information) we'll limit our search
             * to handles owned by the current process */

            //Note that any thread handles provided to us by CREATE_THREAD_DEBUG_EVENT notifications are owned by Kernel32; we don't need to close these,
            //these will automatically be closed after the EXIT_PROCESS_DEBUG_EVENT notification has been received.

            ThreadHandleInfo[] handles = null;

            try
            {
                handles = HandleInfo.EnumerateHandles(Kernel32.GetCurrentProcessId()).OfType<ThreadHandleInfo>().Where(i => i.ThreadProcessId == Id).ToArray();
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.Assert(false, $"Failed to enumerate thread handles: {ex.Message}");
#else
                //In production, don't make any noise, just cross our fingers and see what happens
                return;
#endif
            }

            if (handles.Length > 0)
            {
                //Note: we don't know which threads are owned by Kernel32 vs which ones might be owned by us!
                throw new InvalidOperationException($"Cannot terminate when {handles.Length} thread handles owned by process {Id} are still open");
            }
        }

        #region IDbgProcess

        private ExternalDbgThreadStore externalThreadStore;

        /// <inheritdoc />
        IDbgThreadStore IDbgProcess.Threads => externalThreadStore ??= new ExternalDbgThreadStore(Threads);

        private ExternalDbgModuleStore externalModuleStore;

        /// <inheritdoc />
        IDbgModuleStore IDbgProcess.Modules => externalModuleStore ??= new ExternalDbgModuleStore(Modules);

        #endregion

        public void Dispose()
        {
            if (disposed)
                return;

            Log.Debug<CordbProcess>("Disposing CordbProcess");

            Threads.Dispose();
            DAC.Dispose();
            Symbols.Dispose();

            disposed = true;
        }
    }
}
