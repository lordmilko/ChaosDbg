using System.Text;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public class TtdRawExceptionEvent : TtdRawEvent
    {
        public TtdRawExceptionEvent(ExceptionEvent @event) : base(@event.position)
        {
        }
    }

    public unsafe class TtdRawDebugPrintEvent : TtdRawExceptionEvent
    {
        public string Message { get; }

        public TtdRawDebugPrintEvent(ExceptionEvent @event, Cursor cursor) : base(@event)
        {
            var size = @event.exception.ExceptionInformation[0];
            var ptr = @event.exception.ExceptionInformation[1];

            byte[] bytes;

            switch (@event.exception.ExceptionCode)
            {
                case NTSTATUS.DBG_PRINTEXCEPTION_C:
                    bytes = cursor.QueryMemoryBuffer(ptr, size, QueryMemoryPolicy.Default);
                    Message = Encoding.ASCII.GetString(bytes);
                    break;

                case NTSTATUS.DBG_PRINTEXCEPTION_WIDE_C:
                    size *= 2;
                    bytes = cursor.QueryMemoryBuffer(ptr, size, QueryMemoryPolicy.Default);
                    Message = Encoding.Unicode.GetString(bytes);
                    break;

                default:
                    throw new UnknownEnumValueException(@event.exception.ExceptionCode);
            }
        }
    }
}
