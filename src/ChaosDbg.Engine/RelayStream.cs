using System;
using System.IO;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a stream that simply relays to another stream.
    /// </summary>
    public class RelayStream : Stream
    {
        public override bool CanRead => Stream.CanRead;
        public override bool CanSeek => Stream.CanSeek;
        public override bool CanWrite => Stream.CanWrite;
        public override long Length => Stream.Length;
        public override long Position
        {
            get => Stream.Position;
            set => Stream.Position = value;
        }

        /// <summary>
        /// Gets the inner stream which this stream encapsulates.
        /// </summary>
        protected Stream Stream { get; }

        protected RelayStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Stream = stream;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Stream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) =>
            Stream.Write(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            Stream.Seek(offset, origin);

        public override void SetLength(long value) =>
            Stream.SetLength(value);

        public override void Flush() =>
            Stream.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Stream.Dispose();
        }
    }
}
