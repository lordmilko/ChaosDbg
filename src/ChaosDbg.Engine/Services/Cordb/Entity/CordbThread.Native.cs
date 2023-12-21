using System;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public partial class CordbThread
    {
        /// <summary>
        /// Provides facilities for interacting with a thread that has never executed managed code.<para/>
        /// If a thread utilizing a <see cref="NativeAccessor"/> ever enters managed code, its accessor
        /// will be replaced with a <see cref="ManagedAccessor"/>.
        /// </summary>
        public class NativeAccessor : ICordbThreadAccessor
        {
            public CordbThread Thread { get; set; }

            public int Id { get; }

            public IntPtr Handle { get; }

            public CordbFrame[] StackTrace => CordbFrameEnumerator.Native.Enumerate(this);

            public NativeAccessor(int id, IntPtr handle)
            {
                Id = id;

                /* Is it safe to store the unmanaged callback's hThread after the callback has ended? Yes!
                 * When WaitForDebugEvent() gets a new event, prior to returning it calls kernel32!SaveThreadHandle
                 * to save the thread handle in a linked list. When EXIT_THREAD_DEBUG_EVENT is received,
                 * kernel32!MarkThreadHandle is called to indicate that the thread needs to be cleaned up - but not yet!
                 * Only when the next kernel32!ContinueDebugEvent is called is the handle actually closed. Thus,
                 * it is perfectly safe for us to store the handle. Also DbgEng stores the handle in ThreadInfo too. */
                Handle = handle;
            }
        }
    }
}
