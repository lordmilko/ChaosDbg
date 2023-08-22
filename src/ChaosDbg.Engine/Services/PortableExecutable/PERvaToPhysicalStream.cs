using System;
using System.IO;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a stream for interacting with <see cref="IPEFile"/> instances capable of translating requested relative
    /// addresses to required physical addresses.
    /// </summary>
    public class PERvaToPhysicalStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => stream.Length;
        public override long Position { get; set; }

        private readonly Stream stream;
        private readonly IPEFile peFile;

        public PERvaToPhysicalStream(Stream stream, IPEFile peFile)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (peFile == null)
                throw new ArgumentNullException(nameof(peFile));

            this.stream = stream;
            this.peFile = peFile;

            stream.Seek(0, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            stream.Read(buffer, offset, count);

        public override long Seek(long rva, SeekOrigin origin)
        {
            if (!peFile.TryGetOffset((int) rva, out var physicalOffset))
                throw new InvalidOperationException($"Failed to resolve RVA {rva} to a physical offset");

            if (origin != SeekOrigin.Begin)
                throw new NotSupportedException($"{nameof(PERvaToPhysicalStream)} currently only supports {nameof(SeekOrigin)}.{nameof(SeekOrigin.Begin)}");

            Position = (long) peFile.OptionalHeader.ImageBase + rva;
            stream.Seek(physicalOffset, SeekOrigin.Begin);
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
