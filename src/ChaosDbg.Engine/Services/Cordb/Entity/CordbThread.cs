using System;
using System.Diagnostics;
using System.Text;
using ChaosLib;
using ClrDebug;
using ThreadState = ClrDebug.ThreadState;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a thread that may or may not have ever executed managed code within a <see cref="CordbProcess"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public partial class CordbThread : IDbgThread
    {
        private static readonly int OFFSETOF__TLS__tls_EETlsData = 2 * IntPtr.Size;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                var type = Type;

                var typeStr = type == null ? null : Type + " ";

                if (!IsManaged)
                    builder.Append($"[Native] {typeStr}{Id}");
                else
                {
                    builder.Append("[Managed] ");
                    builder.Append(typeStr);

                    var accessor = (ManagedAccessor) Accessor;

                    if (accessor.Id == accessor.VolatileOSThreadID)
                        builder.Append(accessor.Id);
                    else
                        builder.Append($"Runtime Id = {accessor.Id}, VolatileOSThreadID = {accessor.VolatileOSThreadID}");
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
        /// Gets the handle of this thread.
        /// </summary>
        public IntPtr Handle => Accessor.Handle;

        /// <summary>
        /// Gets the TEB that is associated with this thread.
        /// </summary>
        [Obsolete("When interop debugging, calls to this API may throw if an OOB event is received notifying the debugger that the thread has terminated. Ensure all calls to this API are wrapped inside ExitProtect()")]
        public RemoteTeb Teb { get; }

        /// <summary>
        /// Gets whether this thread has sent an exit notification to the debugger.
        /// </summary>
        public bool Exited { get; internal set; }

        /// <summary>
        /// Gets whether the thread is currently alive.<para/>
        /// Threads may die prior to an ExitThread() event having occurred, and may even die while the process is broken into.
        /// </summary>
        public bool IsAlive
        {
            get
            {
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
        /// Gets the type of this thread within the CLR.<para/>
        /// If this thread does not have a special type, this property returns null.
        /// </summary>
        public TlsThreadTypeFlag? Type
        {
            get
            {
                /* When interop debugging, we may receive an EXIT_THREAD_DEBUG_EVENT at any time, even when the debuggee is supposed
                 * to be "stopped". Try and bail out at every possible opportunity if we were notified this threat has now terminated,
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

                    if (slotValue == 0)
                    {
                        if (Exited)
                            return null;

                        //OK, maybe it's in ThreadLocalStoragePointer
                        slotValue = Teb.GetTlsPointerValue(tlsIndex);

                        //If we still didn't get anything, the thread isn't related to the CLR
                        if (slotValue == 0)
                            return null;

                        var eeTlsDataAddr = slotValue + extraOffset + OFFSETOF__TLS__tls_EETlsData;

                        if (Exited)
                            return null;

                        var eeTlsDataValue = memoryReader.ReadPointer(eeTlsDataAddr);

                        if (eeTlsDataValue == 0)
                            return null;

                        structAddr = eeTlsDataValue;
                    }
                    else
                        structAddr = slotValue;
#pragma warning restore CS0618 // Type or member is obsolete

                    //In modern versions of .NET Core, this will give us size_t t_ThreadType

                    if (Exited)
                        return null;

                    var value = (TlsThreadTypeFlag) memoryReader.ReadPointer(structAddr + memoryReader.PointerSize * (int) PredefinedTlsSlots.TlsIdx_ThreadType);

                    if (value == 0)
                        return null;

                    return value;
                }, null);
            }
        }

        /// <summary>
        /// Gets whether this thread has ever executed managed code.
        /// </summary>
        public bool IsManaged => Accessor is ManagedAccessor;

        public CordbFrame[] StackTrace => Accessor.StackTrace;

        public CordbThread(ICordbThreadAccessor threadAccessor, CordbProcess process)
        {
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

            CordbFrame[] StackTrace { get; }
        }

        #endregion
    }
}
