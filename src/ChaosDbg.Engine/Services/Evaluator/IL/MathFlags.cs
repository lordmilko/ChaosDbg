using System;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Specifies flags used to determine how mathematical operations should be performed.
    /// </summary>
    [Flags]
    enum MathFlags
    {
        /// <summary>
        /// Numbers should be treated as signed. This flag is for informational purposes only in call sites where it is used; numbers will automatically
        /// be treated as signed unless <see cref="Unsigned"/> is specified.
        /// </summary>
        Signed = 0,

        /// <summary>
        /// Numbers should be treated as unsigned.
        /// </summary>
        Unsigned = 1,

        /// <summary>
        /// Mathematical operations should occur in a <see langword="checked"/> context, throwing an <see cref="OverflowException"/> in the event of a numeric overflow.
        /// </summary>
        Checked = 2
    }
}
