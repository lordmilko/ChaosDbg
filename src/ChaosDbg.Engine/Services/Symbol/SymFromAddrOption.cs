using System;

namespace ChaosDbg.Symbol
{
    [Flags]
    public enum SymFromAddrOption
    {
        None = 0,

        /// <summary>
        /// Specifies that native symbols inside the CLR should be resolved, and that symbols for the CLR should be force loaded even if
        /// the current debugger isn't doing interop debugging.
        /// </summary>
        CLR = 1,

        /// <summary>
        /// Specifies that symbols for managed code addresses should be resolved. Implies <see cref="CLR"/>, ensuring any references to CLR internal
        /// functions are resolved as well.
        /// </summary>
        Managed = 2, //Implies CLR

        /// <summary>
        /// Specifies that native symbols should be inspected. If not specified in conjunction with <see cref="CLR"/>, CLR symbols will only be resolved
        /// if CLR symbols were previously loaded into DbgHelp previously.
        /// </summary>
        Native = 4,

        /// <summary>
        /// Specifies that managed code addresses should be resolved that require the use of SOS APIs that may throw when passed bad addresses (impacting performance).
        /// It is recommended to always specify this option in conjunction with <see cref="Managed"/> so that no-throw resolution techniques can be tried first
        /// before fallingback to the more dangerous ones.
        /// </summary>
        DangerousManaged = 8,

        /// <summary>
        /// Specifies that a fallback native symbol, consisting of a displacement relative to a module, should be retrieved in the event that a proper native symbol
        /// cannot be found. Implies <see cref="Native"/>.
        /// </summary>
        Fallback = 16, //Implies Native

        Thunk = 32, //Implies Managed

        //We opt out of thunk in disasm symbol resolvers, but want to opt in in all other scenarios since the whole point
        //is we have no idea what a piece of memory points to, and we may be trying to query the indirected address already
        Safe = CLR | Managed | Native | Fallback | Thunk,

        All = CLR | Managed | Native | DangerousManaged | Fallback | Thunk
    }
}
