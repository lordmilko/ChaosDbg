using System;
using System.Linq;
using ChaosLib;
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
            public int Id => CorDebugThread.Id;

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

            public TlsThreadTypeFlag? Type
            {
                get
                {
                    //We want to get size_t t_ThreadType

                    var dac = Thread.Process.DAC;

                    //This returns g_TlsIndex, which is set by SetIlsIndex. It seems this is a pointer to ThreadLocalInfo gCurrentThreadInfo
                    var index = dac.SOS.TLSIndex;

                    MemoryReader memoryReader = dac.DataTarget;

                    //I'm not 100% clear how this works exactly; SOS and ClrMD use a different strategy to get this value, but it seems to work regardless

                    var slotValue = Thread.Teb.GetTlsValue(index);
                    var value = (TlsThreadTypeFlag) memoryReader.ReadPointer(slotValue + memoryReader.PointerSize * (int) PredefinedTlsSlots.TlsIdx_ThreadType);

                    return value;
                }
            }

            public CordbFrame[] StackTrace => CordbFrameEnumerator.V3.Enumerate(this).ToArray();

            public ManagedAccessor(CorDebugThread corDebugThread)
            {
                CorDebugThread = corDebugThread;
            }
        }
    }
}
