using System;
using System.IO;
using ChaosLib.Memory;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents a stream capable of reading memory from a target process via DbgEng.
    /// </summary>
    class DbgEngMemoryStream : RemoteMemoryStream
    {
        public static Stream CreateRelative(DebugClient client, long absoluteAddress)
        {
            var inner = new DbgEngMemoryStream(client);
            inner.Seek(absoluteAddress, SeekOrigin.Begin);
            return new RelativeToAbsoluteStream(inner, absoluteAddress);
        }

        /* Objects that store DbgEngMemoryStream may not get disposed. This can cause a big race condition, wherein during application shutdown
         * the NativeLibraryProvider unloads dbgeng.dll, but the finalizer of DbgEngMemoryStream hasn't been called yet, leading to an access violation
         * when it is eventually called and the DebugClient's destructor tries to call Release(). Since any DebugClient that would be capsulated
         * by this class should be owned by the DbgEngSessionInfo, we instead take a weak reference to the DebugClient, which will then magically
         * go away after the DbgEngSessionInfo has been disposed and a GC.Collect() has been run (which NativeLibraryProvider will take care of) */
        private WeakReference<DebugClient> clientRef;

        private DebugClient Client
        {
            get
            {
                if (clientRef.TryGetTarget(out var value))
                    return value;

                throw new InvalidOperationException($"Cannot access {nameof(DebugClient)}: object has been garbage collected");
            }
        }

        public DbgEngMemoryStream(DebugClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            this.clientRef = new WeakReference<DebugClient>(client);
        }
        protected override HRESULT ReadVirtual(long address, int count, out byte[] result) =>
            Client.DataSpaces.TryReadVirtual(Position, count, out result);
    }
}
