using ClrDebug;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Encapsulates a <see cref="sbyte"/>, <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>,
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/> or <see cref="ulong"/> value for storage on
    /// the evaluation stack.
    /// </summary>
    class IntegerSlot : NumericSlot
    {
        public IntegerSlot(object value, CorElementType kind) : base(value, kind)
        {
        }
    }
}
