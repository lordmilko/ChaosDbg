using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    class TtdCallTreeEvent
    {
        public string Name { get; set; }
        public ThreadInfo ThreadInfo { get; }
        public Position Position { get; }

        public TtdCallTreeEvent(ThreadInfo threadInfo, Position position)
        {
            ThreadInfo = threadInfo;
            Position = position;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
