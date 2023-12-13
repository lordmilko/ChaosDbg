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
    /// Encapsulates a <see cref="ClrDebug.CorDebugThread"/> that exists within a <see cref="CordbProcess"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class CordbThread : IDbgThread
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var type = Type == 0 ? "Normal" : Type.ToString();

                if (Id == VolatileOSThreadID)
                    return $"{type}: {Id}";

                return $"{type}: Id = {Id}, VolatileOSThreadID = {VolatileOSThreadID}";
            }
        }

        /// <summary>
        /// Gets the underlying <see cref="ClrDebug.CorDebugThread"/> of this entity.
        /// </summary>
        public CorDebugThread CorDebugThread { get; }

        /// <summary>
        /// Gets the <see cref="CordbProcess"/> to which this thread belongs.
        /// </summary>
        public CordbProcess Process { get; }

        /// <summary>
        /// Gets the ID of this thread in the runtime. This is usually the same as <see cref="VolatileOSThreadID"/>,
        /// however does not change when the managed thread moves to a different OS thread. This is not the same as
        /// the managed thread ID.
        /// </summary>
        public int Id => CorDebugThread.Id;

        /// <summary>
        /// Gets the true OS thread that this managed thread is bound to. If the managed thread moves to a different
        /// OS thread, this value will update accordingly.
        /// </summary>
        public int VolatileOSThreadID => CorDebugThread.VolatileOSThreadID;

        /// <summary>
        /// Gets the handle of this thread.
        /// </summary>
        public IntPtr Handle => CorDebugThread.Handle;

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
                if (!Process.DAC.Threads.TryGetValue(VolatileOSThreadID, true, out var dacThread))
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
        /// Gets the type of this thread.
        /// </summary>
        public TlsThreadTypeFlag Type
        {
            get
            {
                //We want to get size_t t_ThreadType

                //This returns g_TlsIndex, which is set by SetIlsIndex. It seems this is a pointer to ThreadLocalInfo gCurrentThreadInfo
                var index = Process.DAC.SOS.TLSIndex;

                MemoryReader memoryReader = Process.DAC.DataTarget;

                //I'm not 100% clear how this works exactly; SOS and ClrMD use a different strategy to get this value, but it seems to work regardless
                
                var slotValue = Teb.GetTlsValue(index);
                var value = (TlsThreadTypeFlag) memoryReader.ReadPointer(slotValue + memoryReader.PointerSize * (int) PredefinedTlsSlots.TlsIdx_ThreadType);

                return value;
            }
        }

        public CordbFrame[] StackTrace => CordbFrameEnumerator.V3.Enumerate(this).ToArray();

        public CordbThread(CorDebugThread corDebugThread, CordbProcess process)
        {
            CorDebugThread = corDebugThread;
            Process = process;

            if (!Process.DAC.Threads.TryGetValue(VolatileOSThreadID, true, out var dacThread))
                throw new InvalidOperationException("Failed to get DacThread");

            Teb = new RemoteTeb(dacThread.teb, Process.DAC.DataTarget);
        }

        public override string ToString() => Id.ToString();
    }
}
