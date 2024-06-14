using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD <see cref="ExceptionEvent"/> as retrieved from the DbgEng Data Model.
    /// </summary>
    public class TtdModelExceptionEvent : TtdModelEvent
    {
        public ulong ProgramCounter { get; }

        public NTSTATUS Code { get; }

        public uint Flags { get; }

        public ulong RecordAddress { get; }

        public TtdModelExceptionEvent(dynamic @event) : base((object) @event)
        {
            var exception = @event.Exception;

            ProgramCounter = exception.ProgramCounter;
            Code = (NTSTATUS) (uint) exception.Code;
            Flags = exception.Flags;
            RecordAddress = exception.RecordAddress;
        }
    }
}
