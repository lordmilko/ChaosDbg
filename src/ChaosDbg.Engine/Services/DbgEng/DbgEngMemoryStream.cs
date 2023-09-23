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
    class DbgEngMemoryStream : AbsoluteStream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Gets or sets the position of the stream in terms of absolute memory addresses within the target processes memory.
        /// </summary>
        public override long Position { get; set; }

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
                throw new NotImplementedException("Reading memory with an offset is not implemented.");

            var hr = Client.DataSpaces.TryReadVirtual(Position, count, out var value);

            if (hr != HRESULT.S_OK)
                return 0;

            //I don't understand why, but if we pass buffer to TryReadVirtual, buffer's contents
            //won't be seen by the caller, so we have to create a temporary buffer instead
            Array.Copy(value, buffer, count);

            Position += value.Length;

            return value.Length;
        }

        /// <summary>
        /// Seeks to the specified absolute memory address within the target processes memory.
        /// </summary>
        /// <param name="offset">The absolute position within the target processes memory to seek to.</param>
        /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin != SeekOrigin.Begin)
                throw new NotSupportedException($"{nameof(DbgEngMemoryStream)} currently only supports {nameof(SeekOrigin)}.{nameof(SeekOrigin.Begin)}");

            Position = offset;
            return Position;
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Flush() =>
            throw new NotSupportedException();
    }
}
