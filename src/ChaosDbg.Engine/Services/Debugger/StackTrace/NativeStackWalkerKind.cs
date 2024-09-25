using System;
using ClrDebug.DIA;

namespace ChaosDbg
{
    /// <summary>
    /// Specifies the kind of stack trace provider that should be used for unwinding native frames.
    /// </summary>
    public enum NativeStackWalkerKind
    {
        /// <summary>
        /// Unwind frames using DbgHelp, via dbghelp!StackWalkEx
        /// </summary>
        DbgHelp,

        /// <summary>
        /// Unwind frames using DIA, via <see cref="DiaStackWalker"/>.
        /// </summary>
        [Obsolete("DIA stack traces do not currently work properly. Due to the radically different SP, DIA native frames do not get deduped with managed frames")]
        DIA
    }
}
