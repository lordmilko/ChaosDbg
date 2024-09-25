using System;
using System.Collections.Generic;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public partial class CordbThread
    {
        /// <summary>
        /// Provides facilities for interacting with a thread that has executed managed code on at least one occassion.
        /// </summary>
        public class ManagedAccessor : ICordbThreadAccessor, IDisposable
        {
            public string Name { get; private set; }

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
            public IntPtr Handle { get; }

            private bool ownsHandle;

            public IEnumerable<CordbFrame> EnumerateFrames(NativeStackWalkerKind nativeStackWalkerKind) => CordbFrameEnumerator.V3.Enumerate(this, nativeStackWalkerKind);

            public ManagedAccessor(CorDebugThread corDebugThread)
            {
                CorDebugThread = corDebugThread;

                //We need to get this ID to remove the thread from the CordbThreadStore,
                //however in rare scenarios the object will already be neutered and
                //asking for it from the CorDebugThread will fail
                Id = CorDebugThread.Id;

                var hr = corDebugThread.TryGetHandle(out var hThread);

                switch (hr)
                {
                    case HRESULT.S_OK:
                        //If we've stopped at an unmanaged event, we won't be able to receive our thread handle as we won't
                        //be synchronized
                        Handle = hThread;
                        break;

                    case HRESULT.E_NOTIMPL:
                        //In V3, we have to provide the thread handle ourselves
                        Handle = Kernel32.OpenThread(ThreadAccess.THREAD_ALL_ACCESS, false, Id);
                        ownsHandle = true;
                        break;

                    default:
                        hr.ThrowOnNotOK();
                        break;
                }
            }

            internal void RefreshName()
            {
                //todo: if we cant get a clr name try an OS one? or maybe reserve that for the native accessor only?
                var value = CordbValue.New(CorDebugThread.Object, Thread);

                if (!value.IsNull)
                    Name = ((CordbStringValue) ((CordbObjectValue) value)["m_Name"]).ClrValue;
                else
                    Name = null;
            }

            public void Dispose()
            {
                if (ownsHandle)
                    Kernel32.CloseHandle(Handle);
            }
        }
    }
}
