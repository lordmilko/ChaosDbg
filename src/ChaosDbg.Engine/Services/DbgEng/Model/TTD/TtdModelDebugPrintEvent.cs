namespace ChaosDbg.DbgEng.Model
{
    public class TtdModelDebugPrintEvent : TtdModelExceptionEvent
    {
        public string Message { get; }

        public TtdModelDebugPrintEvent(dynamic @event, dynamic debugPrintInfo) : base((object) @event)
        {
            Message = debugPrintInfo.Message;
            Type = TtdModelEventType.DebugPrint;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
