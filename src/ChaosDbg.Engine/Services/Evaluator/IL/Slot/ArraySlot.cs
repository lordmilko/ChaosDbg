using System;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Represents a slot capable of storing an array.
    /// </summary>
    class ArraySlot : Slot
    {
        public Type Type { get; }

        public new Array Value => (Array) base.Value;

        public int Length => Value.Length;

        public ArraySlot(object value, Type type) : base(value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Type = type;
        }
    }
}
