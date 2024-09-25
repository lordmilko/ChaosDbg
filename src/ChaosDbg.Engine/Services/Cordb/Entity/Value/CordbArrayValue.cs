using System.Reflection;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed array from the address space of a debug target.
    /// </summary>
    class CordbArrayValue : CordbValue
    {
        /// <summary>
        /// Gets the <see cref="ClrDebug.CorDebugArrayValue"/> that underpins this object.
        /// </summary>
        public new CorDebugArrayValue CorDebugValue => (CorDebugArrayValue) base.CorDebugValue;

        internal CordbArrayValue(CorDebugArrayValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol) : base(corDebugValue, thread, parent, symbol)
        {
        }
    }
}
