using System;
using System.Diagnostics;
using System.Linq;
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
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                var type = Type;

                if (type == null)
                    builder.Append($"[Native] {Id}");
                else
                {
                    builder.Append("[Managed] ");
                    builder.Append(type == 0 ? "Normal" : Type.ToString());
                    builder.Append(": ");

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
        public RemoteTeb Teb { get; }

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
        /// If this is a native thread, this property returns null.
        /// </summary>
        public TlsThreadTypeFlag? Type => Accessor.Type;

        /// <summary>
        /// Gets whether this thread has ever executed managed code.
        /// </summary>
        public bool IsManaged => Accessor is ManagedAccessor;

        public CordbFrame[] StackTrace => Accessor.StackTrace;

        public CordbThread(ICordbThreadAccessor threadAccessor, CordbProcess process)
        {
            Accessor = threadAccessor;
            Process = process;
            Teb = RemoteTeb.FromThread(Handle, Process.DAC.DataTarget);
        }

        public override string ToString() => Id.ToString();

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

            /// <inheritdoc cref="CordbThread.Type" />
            TlsThreadTypeFlag? Type { get; }

            CordbFrame[] StackTrace { get; }
        }

        #endregion
    }
}
