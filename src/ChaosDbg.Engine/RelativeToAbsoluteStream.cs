using System.IO;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a stream that converts relative positions to absolute positions required by an inner <see cref="AbsoluteStream"/>.
    /// </summary>
    class RelativeToAbsoluteStream : RelayStream
    {
        /// <summary>
        /// Gets the base address with which relative addresses should be added to.
        /// </summary>
        public long BaseAddress { get; }

        public override long Position
        {
            get => Stream.Position - BaseAddress;
            set => Stream.Position = BaseAddress + value;
        }

        public RelativeToAbsoluteStream(AbsoluteStream stream, long baseAddress) : base(stream)
        {
            BaseAddress = baseAddress;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            Stream.Seek(BaseAddress + offset, origin);
    }
}
