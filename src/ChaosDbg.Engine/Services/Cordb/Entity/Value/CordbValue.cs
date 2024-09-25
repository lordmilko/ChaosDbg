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
        
    }
}
