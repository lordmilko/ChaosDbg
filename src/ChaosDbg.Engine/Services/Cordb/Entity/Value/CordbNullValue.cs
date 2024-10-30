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
        internal CordbNullValue(CorDebugValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? sourceMember) : base(corDebugValue, thread, parent, sourceMember)
        {
        }

        public override string ToString()
        {
            if (SourceMember == null)
                return "<null>";

            return $"{SourceMember} = null";
        }
    }
}
