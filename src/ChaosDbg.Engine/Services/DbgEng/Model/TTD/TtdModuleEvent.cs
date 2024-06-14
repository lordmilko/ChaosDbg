namespace ChaosDbg.DbgEng.Model
{
    public abstract class TtdModuleEvent : TtdModelEvent
    {
        public ulong Address { get; }
        public uint Checksum { get; }
        public string Name { get; }
        public ulong Size { get; }
        public uint Timestamp { get; }

        protected TtdModuleEvent(dynamic @event) : base((object) @event)
        {
            var module = @event.Module;

            Address = module.Address;
            Checksum = module.Checksum;
            Name = module.Name;
            Size = module.Size;
            Timestamp = module.Timestamp; //Not sure what this format is; I thought it might be Unix time, but for some timestamps we get a crazy value so that doesn't seem right
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
