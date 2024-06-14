using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public abstract class TtdRawEvent
    {
        public Position Position { get; }

        protected TtdRawEvent(Position position)
        {
            Position = position;
        }
    }
}
