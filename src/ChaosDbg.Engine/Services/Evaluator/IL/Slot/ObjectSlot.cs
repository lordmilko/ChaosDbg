using System;

namespace ChaosDbg.Evaluator.IL
{
    class ObjectSlot : Slot
    {
        public Type Type { get; }

        public ObjectSlot(object value, Type type) : base(value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Type = type;
        }
    }
}
