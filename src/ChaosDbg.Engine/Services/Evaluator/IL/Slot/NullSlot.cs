namespace ChaosDbg.Evaluator.IL
{
    class NullSlot : Slot
    {
        //Normally we prohibit passing null as the value (in order to catch bugs), but in this case we allow it
        public NullSlot() : base(null)
        {
        }
    }
}
