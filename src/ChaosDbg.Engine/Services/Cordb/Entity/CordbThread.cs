using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ChaosLib;
using ClrDebug;
using ThreadState = ClrDebug.ThreadState;
using Win32Process = System.Diagnostics.Process;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a thread that may or may not have ever executed managed code within a <see cref="CordbProcess"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public partial class CordbThread : IDbgThread, IDisposable
    {
        private static readonly int OFFSETOF__TLS__tls_EETlsData = 2 * IntPtr.Size;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                var type = SpecialType;

                var typeStr = type == null ? null : SpecialType + " ";
                var activeStr = Process.Threads.ActiveThread == this ? " *" : null;

                if (!IsManaged)
                    builder.Append($"[Native] {typeStr}{Id}{activeStr}");
                else
                {
                    builder.Append("[Managed] ");
                    builder.Append(typeStr);

                    var accessor = (ManagedAccessor) Accessor;

                    if (accessor.CorDebugThread.TryGetVolatileOSThreadID(out var volatileOSThreadId) != S_OK || accessor.Id == volatileOSThreadId)
                        builder.Append(accessor.Id);
                    else
                        builder.Append($"Runtime Id = {accessor.Id}, VolatileOSThreadID = {volatileOSThreadId}");

                    builder.Append(activeStr);
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> to which this thread belongs.
        /// </summary>
        public CordbProcess Process { get; }

        /// <summary>
        /// Gets the operating system ID of this thread.
        /// </summary>
        public int Id => Accessor.Id;

        /// <summary>
        /// Gets the unique ID of the thread inside the debugger engine.
        /// </summary>
        public int UserId { get; }

        /// <summary>
        /// Gets the handle of this thread.
        /// </summary>
        public IntPtr Handle => Accessor.Handle;

        /// <summary>
        /// Gets the TEB that is associated with this thread.
        /// </summary>
        [Obsolete("When interop debugging, calls to this API may throw if an OOB event is received notifying the debugger that the thread has terminated. Ensure all calls to this API are wrapped inside ExitProtect()")]
        public RemoteTeb Teb { get; }

        /// <summary>
        /// Gets whether this thread has received an exit notification from a debugger event callback.
        /// </summary>
        public bool Exited { get; internal set; }

        /// <summary>
        /// Gets whether the thread is still alive, as seen by the operating system.
        /// </summary>
        public bool IsAlive
        {
            get
            {
                /* When we receive the EXIT_THREAD_DEBUG_EVENT notification, the thread is still running, and is inside of NtTerminateThread.
                 * WaitForSingleObject is supposed to tell you when a thread ends, but it doesn't - maybe it's because what we have is a copy
                 * of the handle to the thread? GetExitCodeThread also apparently isn't reliable. Even calling GetThreadId on the handle of the
                 * terminated thread doesn't work. As such, we seem to have no choice but to enumerate the threads of the target process, and
                 * CreateToolhelp32Snapshot is very annoying to work with as it can sometimes spuriously fail; thus, we are forced to use
                 * System.Diagnostics.Process instead */
                return Win32Process.GetProcessById(Process.Id).Threads.Cast<ProcessThread>().Any(t => t.Id == Id);
            }
        }

        /// <summary>
        /// Gets whether the thread is currently alive as reported by the DAC.<para/>
        /// Threads may die prior to an ExitThread() event having occurred, and may even die while the process is broken into.
        /// </summary>
        public bool DacIsAlive
        {
            get
            {
                if (!Process.Session.IsCLRLoaded)
                    return false;

                if (!Process.DAC.Threads.TryGetValue(Id, true, out var dacThread))
                    return false;

                var state = dacThread.state;

                if ((state & ThreadState.TS_Dead) != 0)
                    return false;

                /* Any attempt to access most members of CorDebugThread will do a check against EnsureThreadIsAlive().
                 * This check ultimately goes back to Thread::GetSnapshotState(), which checks whether the flag TS_ReportDead
                 * is set, and if so sets TS_Dead as well. Threads can die even when the process has halted and managed callbacks
                 * are not being pumped. To avoid tripping over attempts to access members on CorDebugThread when that thread has
                 * died, we can try and pre-emptively check whether a thread is actually dead prior to attempting to interact with it,
                 * but even this is fraught with peril - the thread could potentially die right after we checked it!
                 *
                 * The impetus for adding this property was that stack traces that initially succeeded when the debugger was stopped would
                 * then suddenly start failing. How can this be? Is there some other issue we need to detect? In every instance, we found
                 * the issue was indeed caused by the thread deciding it's time was up.
                 *
                 * I don't think dnSpy has this issue, because, for the most part, it seems to enumate all threads' stacks
                 * immediately upon breaking into the process. ChaosDbg will be quite different however, as it will allow
                 * executing completely arbitrary commands and interacting with threads in completely unknown ways.
                 *
                 * mdbg also doesn't suffer from this issue because it caches all thread frames during the current break in.
                 * As such, even if a thread ends up dying, subsequent attempts at displaying a stack trace will rely on the
                 * previously cached frame data. */

                if ((state & ThreadState.TS_ReportDead) != 0)
                    return false;

                //Must be alive then.
                return true;
            }
        }

        /// <summary>
        /// Gets the information about the thread that is stored in the DAC.<para/>
        /// If the thread is not currently visible to the DAC, this property returns null.
        /// </summary>
        internal DacpThreadData? DacThreadData
        {
            get
            {
                if (Process.DAC.Threads.TryGetValue(Id, false, out var dacThread))
                    return dacThread;

                return null;
            }
        }

        #region Type / State

        /// <summary>
        /// Gets the type of this thread within the CLR, if it has a special type.<para/>
        /// If this thread does not have a special type, or the CLR has not yet been loaded,
        /// this property returns null.
        /// </summary>
        public TlsThreadTypeFlag? SpecialType
        {
            get
            {
                //Can't create SOS before we've loaded the CLR
                if (Process.Session.EventHistory.ManagedEventCount == 0)
                    return null;

                /* When interop debugging, we may receive an EXIT_THREAD_DEBUG_EVENT at any time, even when the debuggee is supposed
                 * to be "stopped". Try and bail out at every possible opportunity if we were notified this thread has now terminated,
                 * and wrap in a try/catch that will do a hard check whether the thread is actually running or not and rethrow on failure
                 * if the thread is in fact still running */

                return ExitProtect<TlsThreadTypeFlag?>(() =>
                {
                    /* A thread's special type (if applicable) is stored in thread local storage. There are two types
                     * of thead local storage:
                     *
                     * 1. Storage that is dynamically allocated at runtime via TlsAlloc / TlsSetValue are found in
                     *    TlsSlots / TlsExpansionSlots
                     * 2. Storage that is defined at compile while storage that is defined at compile time and embedded
                     *    in the PE image is accessible via the ThreadLocalStoragePointer member
                     *
                     * Historically, a thread's type was defined in TlsSlots. However, around the time of .NET Core 1.0,
                     * Microsoft embarked upon an project called FEATURE_IMPLICIT_TLS wherein TLS values would now be accessed
                     * via ThreadLocalStoragePointer. Complicating matters even further, some time after this, the "TLS index"
                     * that SOS reports was added to the top 15 bits (the top most bit is then also set to 1)
                     *
                     *     SetIlsIndex((DWORD)(_tls_index + (offsetOfCurrentThreadInfo << 16) + 0x80000000));
                     *
                     * This is not the end of the world however, because if these high bits aren't set, this simply means that
                     * there won't be an extra offset that we need to skip over. */

                    var dac = Process.DAC;
                    MemoryReader memoryReader = dac.DataTarget;

                    //In .NET Core, this returns g_TlsIndex, which is set by SetIlsIndex. It seems this is a pointer to ThreadLocalInfo gCurrentThreadInfo
                    var rawIndex = dac.SOS.TLSIndex;

                    //Regardless of whether TlsSlots or ThreadLocalStoragePointer is used, the bottom 16 bits will contain the index
                    var tlsIndex = rawIndex & 0xFFFF;

                    //Grab bits 8-15 (ignoring bit 16). If the TLS data is in TlsSlots, this will be 0
                    var extraOffset = (rawIndex & 0x7FFF0000) >> 16;

#pragma warning disable CS0618 // Type or member is obsolete
                    if (Exited)
                        return null;

                    var slotValue = Teb.GetTlsSlotValue(tlsIndex);

                    long structAddr;

                    bool implicitTLS = false;

                    if (slotValue == 0)
                    {
                        if (Exited)
                            return null;

                        //OK, maybe it's in ThreadLocalStoragePointer (implicit TLS)
                        slotValue = Teb.GetTlsPointerValue(tlsIndex);

                        //If we still didn't get anything, the thread isn't related to the CLR
                        if (slotValue == 0)
                            return null;

                        var eeTlsDataAddr = slotValue + extraOffset + OFFSETOF__TLS__tls_EETlsData;

                        if (Exited)
                            return null;

                        //Threads that were started as native threads may have garbage pointed to by
                        //their ThreadLocalStoragePointer, so we can't trust anything
                        if (memoryReader.TryReadPointer(eeTlsDataAddr, out var eeTlsDataValue) != S_OK)
                            return null;

                        if (eeTlsDataValue == 0)
                            return null;

                        implicitTLS = true;
                        structAddr = eeTlsDataValue;
                    }
                    else
                        structAddr = slotValue;
#pragma warning restore CS0618 // Type or member is obsolete

                    //In modern versions of .NET Core, this will give us size_t t_ThreadType

                    if (Exited)
                        return null;

                    var typeAddr = structAddr + memoryReader.PointerSize * (int) PredefinedTlsSlots.TlsIdx_ThreadType;

                    //Threads that were started as native threads may have garbage pointed to by
                    //their ThreadLocalStoragePointer, so we can't trist anything
                    if (memoryReader.TryReadPointer(typeAddr, out var rawType) != S_OK)
                        return null;

                    //A super mega large value with the highest bit set will be a negative value
                    if (rawType <= 0)
                        return null;

                    if (implicitTLS && !IsManaged)
                    {
                        //If this is a native thread, the trail we followed from the ThreadLocalStoragePointer could be random data
                        //unrelated to the CLR. If the rawType is higher than the highest known valid type, we'll say its bogus

                        var highest = (int) Enum.GetValues(typeof(TlsThreadTypeFlag)).Cast<TlsThreadTypeFlag>().Last();

                        //If every flag is set, the maximum possible value will be (highest * 2) - 1
                        //(e.g. if the highest is 2, flags 1 + 2 being set = 3. (2 * 2) - 1 = 3
                        if (rawType >= (highest * 2))
                            return null;
                    }

                    return (TlsThreadTypeFlag) rawType;
                }, null);
            }
        }

        /// <summary>
        /// Gets the current state of the thread, as reported by the DAC.<para/>
        /// If the thread is not currently visible to the DAC, this property returns null.
        /// </summary>
        public ThreadState? DacState => DacThreadData?.state;

        //Type

        /// <summary>
        /// Gets whether or not this is the main managed thread.
        /// </summary>
        public bool IsMain { get; internal set; }

        public bool IsDebuggerHelper => IsSpecialType(TlsThreadTypeFlag.ThreadType_DbgHelper);

        public bool IsFinalizer => IsSpecialType(TlsThreadTypeFlag.ThreadType_Finalizer);

        public bool IsGC => IsSpecialType(TlsThreadTypeFlag.ThreadType_GC);

        public bool IsShutdownHelper => IsSpecialType(TlsThreadTypeFlag.ThreadType_ShutdownHelper);

        public bool IsSuspendingEE => IsSpecialType(TlsThreadTypeFlag.ThreadType_DynamicSuspendEE);

        public bool IsThreadpoolGate => IsSpecialType(TlsThreadTypeFlag.ThreadType_Gate);

        public bool IsThreadpoolTimer => IsSpecialType(TlsThreadTypeFlag.ThreadType_Timer);

        public bool IsThreadpoolWait => IsSpecialType(TlsThreadTypeFlag.ThreadType_Wait);

        //Type || State

        public bool IsThreadpoolCompletionPort => IsSpecialType(TlsThreadTypeFlag.ThreadType_Threadpool_IOCompletion) || IsState(ThreadState.TS_CompletionPortThread);

        public bool IsThreadpoolWorker => IsSpecialType(TlsThreadTypeFlag.ThreadType_Threadpool_Worker) || IsState(ThreadState.TS_TPWorkerThread);

        //State

        //Helpers

        private bool IsSpecialType(TlsThreadTypeFlag type) => SpecialType?.HasFlag(type) == true;

        private bool IsState(ThreadState state) => DacState?.HasFlag(state) == true;

        #endregion

        /// <summary>
        /// Gets whether this thread has ever executed managed code.
        /// </summary>
        public bool IsManaged => Accessor is ManagedAccessor;

        public IEnumerable<CordbFrame> EnumerateFrames() => Accessor.EnumerateFrames();

        public CordbFrame[] StackTrace => EnumerateFrames().ToArray();

        private CrossPlatformContext registerContext;

        /// <summary>
        /// Gets the current register context of this thread.<para/>
        /// The returned context is a full context with all registers populated.
        /// </summary>
        public CrossPlatformContext RegisterContext
        {
            get
            {
                if (registerContext == null)
                {
                    //If you look at the registers requested in dbgeng!Amd64MachineInfo::InitializeContextFlags, it's basically just AMD64ContextAll/
                    //after factoring in that we're in user mode
                    var flags = Process.Is32Bit ? ContextFlags.X86ContextAll : ContextFlags.AMD64ContextAll;

                    //Don't think we can query CordbProcess::GetThreadContext on the Win32 Event Thread
                    var raw = Process.DataTarget.GetThreadContext<CROSS_PLATFORM_CONTEXT>(
                        Id,
                        flags
                    );

                    registerContext = new CrossPlatformContext(flags, raw);
                }

                return registerContext;
            }
        }

        /// <summary>
        /// Commits any changes that were made to this thread's <see cref="RegisterContext"/>.
        /// </summary>
        /// <param name="clear">Whether to clear the cached context from this thread after saving.</param>
        public void TrySaveRegisterContext(bool clear = false)
        {
            if (registerContext == null)
                return;

            //I don't think we can call CordbProcess::SetThreadContext on the Win32 Event THread
            if (registerContext.IsModified)
                Process.DataTarget.SetThreadContext(Id, registerContext.Raw);

            if (clear)
                registerContext = null;
            else
                registerContext.IsModified = false;
        }

        public CordbThread(int userId, ICordbThreadAccessor threadAccessor, CordbProcess process)
        {
            UserId = userId;
            Accessor = threadAccessor;
            Process = process;

#pragma warning disable CS0618 // Type or member is obsolete
            Teb = RemoteTeb.FromThread(Handle, Process.DAC.DataTarget);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public override string ToString() => Id.ToString();

        private T ExitProtect<T>(Func<T> func, T @default)
        {
            if (Exited)
                return @default;

            try
            {
                return func();
            }
            catch
            {
                if (IsAlive)
                    throw;

                //No longer alive, return default value
                return @default;
            }
        }

        #region ICordbThreadAccessor

        private ICordbThreadAccessor accessor;

        /// <summary>
        /// Gets or sets the accessor that is used to interact with the underlying thread.
        /// </summary>
        public ICordbThreadAccessor Accessor
        {
            get => accessor;
            set
            {
                if (value != null)
                    value.Thread = this;

                accessor = value;
            }
        }

        /// <summary>
        /// Provides facilities for interacting with either a native or managed thread.
        /// </summary>
        public interface ICordbThreadAccessor
        {
            CordbThread Thread { get; set; }

            /// <inheritdoc cref="CordbThread.Id" />
            int Id { get; }

            /// <inheritdoc cref="CordbThread.Handle" />
            IntPtr Handle { get; }

            IEnumerable<CordbFrame> EnumerateFrames();
        }

        #endregion

        public void Dispose()
        {
            if (accessor is IDisposable d)
                d.Dispose();
        }
    }
}
