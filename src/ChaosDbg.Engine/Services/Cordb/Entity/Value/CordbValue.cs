using System;
using System.Diagnostics;
using System.Reflection;
using ChaosLib.Metadata;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a <see cref="CordbValue"/> that has a native CLR representation.
    /// </summary>
    /// <typeparam name="T">The native CLR type that is encapsulated in this value.</typeparam>
    interface ICordbClrValue<T>
    {
        T ClrValue { get; }
    }

    /// <summary>
    /// Represents a managed value from the address space of a debug target.<para/>
    /// This value is only valid during a given instance in which the debugger is paused.
    /// When <see cref="CorDebugController.Continue(bool)"/> is called, this value becomes stale
    /// and may no longer be valid.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class CordbValue
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (Symbol == null)
                    return $"[{GetType().Name}] {ToString()}";

                return ToString();
            }
        }

        internal static CordbValue New(CorDebugValue value, CordbThread thread, CordbValue? parent = null, MemberInfo? symbol = null)
        {
            if (value is CorDebugReferenceValue r)
            {
                if (r.IsNull)
                    return new CordbNullValue(value, thread, parent, symbol);
                else
                    value = r.Dereference();
            }

            if (value is CorDebugBoxValue b)
                value = b.Object;

            if (value.Raw is ICorDebugStringValue)
                return new CordbStringValue(value.As<CorDebugStringValue>(), thread, parent, symbol);

            if (value.Raw is ICorDebugArrayValue)
                return new CordbArrayValue(value.As<CorDebugArrayValue>(), thread, parent, symbol);

            if (value.Raw is ICorDebugObjectValue)
                return new CordbObjectValue(value.As<CorDebugObjectValue>(), thread, parent, symbol);

            if (value.Raw is ICorDebugGenericValue)
                return new CordbPrimativeValue(value.As<CorDebugGenericValue>(), thread, parent, symbol);
        private CorDebugValue corDebugValue;

        /// <summary>
        /// Gets the <see cref="ClrDebug.CorDebugValue"/> that underpins this object.
        /// </summary>
        public CorDebugValue CorDebugValue
        {
            get
            {
                ThrowIfStale();
                return corDebugValue;
            }
        }

        /// <summary>
        /// Gets the <see cref="MetadataPropertyInfo"/> or <see cref="MetadataFieldInfo"/> that this value was retrieved from,
        /// or <see langword="null"/> if this is a root level object.
        /// </summary>
        public MemberInfo? Symbol { get; }

        public bool IsNull
        {
            get
            {
                ThrowIfStale();
                return this is CordbNullValue;
            }
        }

        /// <summary>
        /// Gets the <see cref="CordbThread"/> that the value was retrieved from.
        /// </summary>
        public CordbThread Thread { get; }
        
        /// <summary>
        /// Gets the parent object that this object was retrieved from, or <see langword="null"/> if this is a root level object.
        /// </summary>
        public CordbValue? Parent { get; }

        /// <summary>
        /// Gets whether this value was created during a previous instance in which the debugger was paused,
        /// and is now out of date.
        /// </summary>
        public bool IsStale => Thread.Process.Session.TotalContinueCount != sourceContinueCount;

        private readonly int sourceContinueCount;

        protected CordbValue(CorDebugValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol)
        {
            this.corDebugValue = corDebugValue;
            Thread = thread;
            Parent = parent;
            Symbol = symbol;
            sourceContinueCount = thread.Process.Session.TotalContinueCount;
        }
    }
}
