﻿using ClrDebug.TTD;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD <see cref="ThreadTerminatedEvent"/> as retrieved from the DbgEng Data Model.
    /// </summary>
    public class TtdModelThreadTerminatedEvent : TtdModelEvent
    {
        public TtdModelThreadTerminatedEvent(dynamic @event) : base((object) @event)
        {
        }
    }
}
