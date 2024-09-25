namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Represents a container capable of storing a value type in a "box", allowing it to be stored on the heap.
    /// </summary>
    class BoxSlot : Slot
    {
        /// <summary>
        /// Gets the slot containing the value that is boxed within this slot.
        /// </summary>
        public Slot BoxedSlot { get; }

        public BoxSlot(Slot value) : base(value.Value)
        {
            BoxedSlot = value;
        }
    }
}
