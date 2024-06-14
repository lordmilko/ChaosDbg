using System;
using ClrDebug.TTD;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD <see cref="Position"/> as retrieved from the DbgEng Data Model.
    /// </summary>
    public struct TtdModelPosition : IEquatable<TtdModelPosition>
    {
        public ulong Sequence { get; }
        public ulong Steps { get; }

        private dynamic modelObject;

        public TtdModelPosition(dynamic modelObject)
        {
            Sequence = modelObject.Sequence;
            Steps = modelObject.Steps;

            this.modelObject = modelObject;
        }

        public bool Equals(TtdModelPosition other)
        {
            return Sequence == other.Sequence && Steps == other.Steps;
        }

        public override bool Equals(object obj)
        {
            return obj is TtdModelPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Sequence.GetHashCode();
                hashCode = (hashCode * 397) ^ Steps.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{Sequence:X}:{Steps:X}";
        }
    }
}
