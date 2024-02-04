using System.Collections.Generic;
using ChaosDbg.Analysis;

namespace ChaosDbg.Tests
{
    class ByteSequenceComparer : IEqualityComparer<ByteSequence>
    {
        public static readonly ByteSequenceComparer Instance = new ByteSequenceComparer();

        public bool Equals(ByteSequence x, ByteSequence y)
        {
            if (x.Bytes.Length != y.Bytes.Length)
                return false;

            //Bytes and Masks should always have the same length

            if (x.Mark != y.Mark)
                return false;

            for (var i = 0; i < x.Length; i++)
            {
                if (x.Bytes[i] != y.Bytes[i])
                    return false;

                if (x.Masks[i] != y.Masks[i])
                    return false;
            }

            return true;
        }

        public int GetHashCode(ByteSequence obj)
        {
            var hashCode = 0;

            foreach (var val in obj.Bytes)
                hashCode ^= val.GetHashCode();

            foreach (var val in obj.Masks)
                hashCode ^= val.GetHashCode();

            hashCode ^= obj.Mark.GetHashCode();

            return hashCode;
        }
    }
}
