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

        internal CordbStringValue(CorDebugStringValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol) : base(corDebugValue, thread, parent, symbol)
        {
        }

        public static implicit operator string(CordbStringValue value) => value.ClrValue;

        public override string ToString()
        {
            return $"\"{ClrValue}\"";
        }
    }
}
