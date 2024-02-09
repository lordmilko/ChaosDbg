namespace ChaosDbg.Analysis
{
    class JumpTableMetadataRange : IMetadataRange
    {
        public long FunctionAddress { get; }

        public long StartAddress { get; }

        public long EndAddress { get; }

        public int SlotSize { get; }

        public long[] Slots { get; }

        public long[] Targets { get; }

        public JumpTableMetadataRange(long functionAddress, long address, int slotSize, long[] slots, long[] targets)
        {
            FunctionAddress = functionAddress;
            StartAddress = address;
            EndAddress = (StartAddress + slots.Length * slotSize) - 1; //Subtract 1 so its the last address we occupy, not the next address after us
            SlotSize = slotSize;
            Slots = slots;
            Targets = targets;
        }
    }
}
