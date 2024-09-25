using ClrDebug;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Encapsulates a <see cref="float"/> or <see cref="double"/> value for storage on the evaluation stack.
    /// </summary>
    class FloatingPointSlot : NumericSlot
    {
        public FloatingPointSlot(object value, CorElementType kind) : base(value, kind)
        {
        }
    }
}
