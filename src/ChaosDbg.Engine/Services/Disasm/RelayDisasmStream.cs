using System;
using System.IO;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a <see cref="DisasmStream"/> that relays to an inner stream.
    /// </summary>
    class RelayDisasmStream : DisasmStream
    {
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;
        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        private Stream stream;

        public RelayDisasmStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            this.stream = stream;
        }

        protected override int ReadDisasm(byte[] buffer, int offset, int count) =>
            stream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) =>
            stream.Write(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            stream.Seek(offset, origin);

        public override void SetLength(long value) =>
            stream.SetLength(value);

        public override void Flush() =>
            stream.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                stream.Dispose();
        }
    }
}
