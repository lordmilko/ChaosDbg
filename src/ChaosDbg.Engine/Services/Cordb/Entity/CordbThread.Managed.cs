using System;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public partial class CordbThread
    {
        /// <summary>
        /// Provides facilities for interacting with a thread that has executed managed code on at least one occassion.
        /// </summary>
        public class ManagedAccessor : ICordbThreadAccessor
        {
            public CordbThread Thread { get; set; }

            /// <summary>
            /// Gets the underlying <see cref="ClrDebug.CorDebugThread"/> of this entity.
            /// </summary>
            public CorDebugThread CorDebugThread { get; }

            /// <summary>
            /// Gets the ID of this thread in the runtime. This is usually the same as <see cref="VolatileOSThreadID"/>,
            /// however does not change when the managed thread moves to a different OS thread. This is not the same as
            /// the managed thread ID.
            /// </summary>
            public int Id { get; }

            /// <summary>
            /// Gets the true OS thread that this managed thread is bound to. If the managed thread moves to a different
            /// OS thread, this value will update accordingly.<para/>
            ///
            /// It is not safe to access this value from unmanaged callbacks as the macro ATT_REQUIRE_STOPPED_MAY_FAIL
            /// will attempt to call CordbProcess::StartSyncFromWin32Stop. This method tries to synchronize the process against
            /// the in-proc debugger. If we are on the Win32 event thread in an unmanaged callback, this will block mscordbi
            /// attempting to perform its required async break.
            /// </summary>
            public int VolatileOSThreadID => CorDebugThread.VolatileOSThreadID;

            /// <inheritdoc cref="CordbThread.Handle" />
            public IntPtr Handle => CorDebugThread.Handle;

            public CordbFrame[] StackTrace => CordbFrameEnumerator.V3.Enumerate(this).ToArray();

            public ManagedAccessor(CorDebugThread corDebugThread)
            {
                CorDebugThread = corDebugThread;

                //We need to get this ID to remove the thread from the CordbThreadStore,
                //however in rare scenarios the object will already be neutered and
                //asking for it from the CorDebugThread will fail
                Id = CorDebugThread.Id;
            }
        }
    }
}
