using System;
using System.IO;
using ChaosLib.Memory;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a stream capable of reading memory from a target process via an <see cref="ICLRDataTarget"/>.
    /// </summary>
    class CordbMemoryStream : RemoteMemoryStream
    {
        public static RelativeToAbsoluteStream CreateRelative(ICLRDataTarget dataTarget, long absoluteAddress)
        {
            var inner = new CordbMemoryStream(dataTarget);
            inner.Seek(absoluteAddress, SeekOrigin.Begin);
            return new RelativeToAbsoluteStream(inner, absoluteAddress);
        }

        private ICLRDataTarget dataTarget;

        public CordbMemoryStream(ICLRDataTarget dataTarget)
        {
            if (dataTarget == null)
                throw new ArgumentNullException(nameof(dataTarget));

            this.dataTarget = dataTarget;
        }

        protected override HRESULT ReadVirtual(long address, int count, out byte[] result) =>
            dataTarget.TryReadVirtual(address, count, out result);
    }
}
