using System;

namespace ChaosDbg.DbgEng.Model
{
    public enum TtdModelEventType
    {
        ModuleLoaded,
        ModuleUnloaded,
        ThreadCreated,
        ThreadTerminated,
        Exception,

        //Special subclass of exception
        DebugPrint
    }

    public abstract class TtdModelEvent
    {
        public TtdModelEventType Type { get; protected set; }

        public TtdModelPosition Position { get; }

        private dynamic modelObject;

        protected TtdModelEvent(dynamic @event)
        {
            Type = (TtdModelEventType) Enum.Parse(typeof(TtdModelEventType), (string) @event.Type);
            Position = new TtdModelPosition(@event.Position);

            this.modelObject = @event;
        }
    }
}
