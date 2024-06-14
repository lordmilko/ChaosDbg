using ClrDebug.TTD;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD <see cref="ModuleUnloadedEvent"/> as retrieved from the DbgEng Data Model.
    /// </summary>
    public class TtdModelModuleUnloadedEvent : TtdModuleEvent
    {
        public TtdModelModuleUnloadedEvent(dynamic @event) : base((object) @event)
        {
        }
    }
}
