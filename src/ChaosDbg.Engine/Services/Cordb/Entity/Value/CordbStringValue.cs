using System.Reflection;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="string"/> in the address space of a debug target.
    /// </summary>
    class CordbStringValue : CordbValue, ICordbClrValue<string>
    {
        /// <summary>
        /// Gets the <see cref="string"/> value that is associated with this object.
        /// </summary>
        public string ClrValue
        {
            get
            {
                ThrowIfStale();
                return CorDebugValue.GetString(CorDebugValue.Length);
            }
        }

        public new CorDebugStringValue CorDebugValue => (CorDebugStringValue) base.CorDebugValue;

        internal CordbStringValue(CorDebugStringValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? sourceMember) : base(corDebugValue, thread, parent, sourceMember)
        {
        }

        public override bool IsEquivalentTo(object other)
        {
            if (other == null)
                return false;

            if (other is CordbStringValue v)
                return Equals(v.ClrValue, ClrValue);

            if (other is string s)
                return s.Equals(ClrValue);

            return false;
        }

        public static implicit operator string(CordbStringValue value) => value.ClrValue;

        public override string ToString()
        {
            return $"\"{ClrValue}\"";
        }
    }
}
