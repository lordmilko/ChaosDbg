using ClrDebug.TTD;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD <see cref="ModuleLoadedEvent"/> as retrieved from the DbgEng Data Model.
    /// </summary>
    public class TtdModelModuleLoadedEvent : TtdModuleEvent
    {
        public TtdModelModuleLoadedEvent(dynamic @event) : base((object) @event)
        {
        }
    }
}
