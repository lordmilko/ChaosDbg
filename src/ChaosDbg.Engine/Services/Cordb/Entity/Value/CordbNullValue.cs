using System.Reflection;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see langword="null"/> value from the address space of a debug target.
    /// </summary>
    class CordbNullValue : CordbValue
    {
        internal CordbNullValue(CorDebugValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol) : base(corDebugValue, thread, parent, symbol)
        {
        }

        public override string ToString()
        {
            if (Symbol == null)
                return "<null>";

            return $"{Symbol} = null";
        }
    }
}
