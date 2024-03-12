using System.Diagnostics;

namespace ChaosDbg.Cordb
{
    /* CLR Breakpoints
     * ---------------
     * There are several types of code breakpoints that you can set in a managed process. You can set breakpoints
     *
     * - in managed code (IL)
     * - in managed code (disassembly)
     * - in regular native code
     * - inside the CLR
     *
     * and each of these is handled very differently by the CLR. In summary, setting breakpoints inside managed code is done with the cooperation of both
     * the "left" and "right sides", while setting breakpoints inside regular native code, or inside of the CLR itself, is done purely inside mscordbi
     *
     * mscordbi maintains two lists of patches
     * - m_NativePatchList: contains all native patches that have been applied directly against native code (CordbProcess::SetUnmanagedBreakpoint)
     * - m_pPatchTable:     contains all patches (whether against IL or native code) that have been applied via CordbCode::CreateBreakpoint that the
     *                      left side (the in-process Debugger) has created and that the right side (mscordbi) has directly read out of the remote process.
     *                      Initialized from DebuggerIPCRuntimeOffsets.m_pPatches <- DebuggerPatchTable::GetPatchTable()
     *
     * Native Breakpoints
     * ------------------
     * Breakpoints in regular native code can be set with mscordbi!CordbProcess::SetUnmanagedBreakpoint(). Upon doing this, mscordbi!CordbProcess::m_NativePatchList will be updated
     * to say that we've added an int3 at this address.
     *
     * Inside process.cpp!CordbWin32EventThread::Win32EventLoop, upon receiving an event, it will be dispatched to ShimProcess::HandleWin32DebugEvent -> CordbProcess::HandleDebugEventForInteropDebugging
     * where the method CordbProcess::TriageWin32DebugEvent -> CordbProcess::TriageExcep1stChanceAndInit will then be called. This method is very important, as it basically gives mscordbi
     * an opportunity to intercept any exceptions and determine that ICorDebug does not need to see them. It does this by determining a "Reaction" to have to the exception. Once TriageWin32DebugEvent
     * returns back to HandleDebugEventForInteropDebugging, it will handle the exception, dispatching it to ICorDebug if it was determined to either be cInband or cOOB.
     *
     * What happens if you then try and set a breakpoint inside the CLR?
     *
     * If you set a breakpoint inside the CLR (either on an internal CLR thread or a thread used for executing managed code), during TriageExcep1stChanceAndInit one of the checks it performs
     * is whether a patch via CordbProcess::SetUnmanagedBreakpoint has been created (CordbProcess::GetNativePatch). If so, it checks if the CordbUnmanagedThread (that the exception's thread Id
     * was resolved to to earlier inside HandleDebugEventForInteropDebugging) returns true for CordbUnmanagedThread::IsCantStop(). IsCantStop performs several checks for things that aren't allowed
     * to stop (including checking whether we're trying to break into special helper threads used by mscordbi), but in the case of a CLR internal breakpoint, we trip over the last check that is
     * performed: CordbUnmanagedThread::GetEEPGCDisabled(). This remotely queries the clr!Thread::m_fPreemptiveGCDisabled to see whether the current thread is in cooperative mode or not. Typicaly,
     * a thread that is executing in cooperative mode is executing managed code, while a thread that is executing regular native code executes in preemptive mode. When EEThreadPGCDisabled is 1,
     * IsCantStop() returns true, resulting in CordbUnmanagedThread::SetupForSkipBreakpoint() being called to skip over the breakpoint, single step, and then reinstate the illegal breakpoint
     * via CordbUnmanagedThread::FixupForSkipBreakpoint().
     *
     * Based on this, you might think that, if you set a breakpoint inside the CLR yourself, without the help of CordbProcess::SetUnmanagedBreakpoint(), you'll be able to trick
     * HandleDebugEventForInteropDebugging into letting it fly. Not so fast, because when TriageExcep1stChanceAndInit fails to find a special meaning for the breakpoint, it'll default
     * to calling Triage1stChanceNonSpecial which, seeing that the exception is STATUS_BREAKPOINT, will call TriageBreakpoint with the assumption that it must be a breakpoint that
     * was set against managed code (either at the IL or native disassembly level). It calls CordbProcess::FindPatchByAddress() to try and find a breakpoint in CordbProcess:m_pPatchTable
     * (which is force cleared and re-read from clr!Debugger to ensure that its up to date). But a patch won't be found, forcing mscordbi to give up and send us the exception.
     * It does one more check however, sees that the current thread IsCantStop() and on that basis sends us the event
     * as a cOOB event.
     *
     * Thus, we can see that in order to set a breakpoint inside the CLR, we need to do two things:
     * 1. Set the breakpoint ourselves, without the help of CordbProcess::SetUnmanagedBreakpoint
     * 2. Don't automatically continue when we get the OOB breakpoint event like we normally would, and do
     *    some bookkeeping so that when we do continue, we throw an OOB continue in there first.
     *
     * If there's 
     *
     * Managed Breakpoints
     * -------------------
     *
     * Setting breakpoints inside of managed code is handled completely differently to how native breakpoints are handled. Managed breakpoints are almost entirely handled inside of
     * the remote process itself, without the help of the debugger. How can a process catch its own int3? The answer should be obvious: there's a global exception handler.
     *
     * The IL or native code of a managed method can be retrieved via CordbFunction::GetILCode() and CordbFunction::GetNativeCode() respectively. When CordbCode::CreateBreakpoint() is called,
     * it will store whether the specified offset relates to IL or native code. CordbFunctionBreakpoint::Activate() is then automatically called, which causes an DB_IPCE_BREAKPOINT_ADD
     * event to be sent to the clr!Debugger which adds the breakpoint into its in memory patch list (e.g. AddILPatch/AddBindAndActivateNativeManagedPatch).
     *
     * In EEStartupHelper() it calls InitializeExceptionHandling() -> CLRAddVectoredHandlers() which then calls ::AddVectoredExceptionHandler() with CLRVectoredExceptionHandlerShim.
     * This installs a global exception handler. When activated, it calls CLRVectoredExceptionHandler -> CLRVectoredExceptionHandlerPhase2 -> IsDebuggerFault ->
     * g_pDebugInterface->FirstChanceNativeException, thus allowing it to call DebuggerController::DispatchNativeException and send us an DB_IPCE_BREAKPOINT event.
     *
     * The one remaining question here is: even if it has its own built-in exception handler, shouldn't the debugger still get a first chance exception notification anyway? The answer is yes,
     * they're just being suppressed!
     *
     * As discussed above, when an unmanaged breakpoint has been set via CordbProcess::SetUnmanagedBreakpoint(), this will be discovered by
     *
     *     TriageExcep1stChanceAndInit -> GetNativePatch
     *
     * When there's no special mscordbi meaning to a breakpoint, the fact it's a clr!Debugger internal breakpoint is discovered by
     *
     *     TriageExcep1stChanceAndInit -> Triage1stChanceNonSpecial -> TriageBreakpoint -> FindPatchByAddress
     *
     * In the case of a managed breakpoint, regardless of what kind of breakpoint it is (it seems like the TraceDestination.type
     * will most likely either be set to TRACE_UNMANAGED or DPT_DEFAULT_TRACE_TYPE (TRACE_OTHER)) TriageBreakpoint will return cCLR, 
     * which HandleDebugEventForInteropDebugging will understand means we don't need to do anything and can ignore the exception (without
     * informing ICorDebug) because the unmanaged exception handler inside of the managed process will take care of everything. And so,
     * it calls ForceDbgContinue with DBG_EXCEPTION_NOT_HANDLED, sending the exception handling back to the managed process.
     */

    /// <summary>
    /// Represents a breakpoint that is activated from either a managed or an unmanaged event.
    /// </summary>
    public abstract class CordbBreakpoint : IDbgBreakpoint
    {
        /// <summary>
        /// Gets whether the user has currently enabled or disabled this breakpoint.
        /// </summary>
        public bool IsEnabled { get; protected set; }

        /// <summary>
        /// Gets or sets whether this breakpoint has been temporarily suspended so that the original CPU instruction
        /// it overwrote can be executed.<para/>
        /// This property is distinct from <see cref="IsEnabled"/>, which specifies whether the user has
        /// enabled or disabled this breakpoint or not.
        /// </summary>
        public bool IsSuspended { get; protected set; }

        public bool IsOneShot { get; }

        protected CordbBreakpoint(bool isOneShot)
        {
            IsOneShot = isOneShot;
        }

        /// <summary>
        /// Sets whether the breakpoint should be enabled or disabled. This is controlled by the user.
        /// </summary>
        /// <param name="enable"></param>
        public void SetEnabled(bool enable)
        {
            if (!enable && IsSuspended)
            {
                //If the user is trying to disable a breakpoint that we've suspended, remove the suspension
                IsSuspended = false;
            }

            Debug.Assert(IsEnabled != enable);
            Activate(enable);
            IsEnabled = enable;
        }

        /// <summary>
        /// Temporarily suspends or this breakpoint so that the debugger can skip past it,
        /// or resumes the breakpoint if it was previously suspended.
        /// </summary>
        /// <param name="suspend">Whether to suspend or resume the breakpoint.</param>
        public void SetSuspended(bool suspend)
        {
            Debug.Assert(IsSuspended != suspend);
            Activate(!suspend);
            IsSuspended = suspend;
        }

        protected abstract void Activate(bool activate);
    }
}
