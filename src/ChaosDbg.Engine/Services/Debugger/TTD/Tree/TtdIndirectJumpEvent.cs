using System.Diagnostics;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    [DebuggerDisplay("[{Position.ToString(),nq}] Name = {Name}, Address = {Address}")]
    class TtdIndirectJumpEvent : TtdCallTreeEvent
    {
        public GuestAddress Address { get; }

        public TtdIndirectJumpEvent(GuestAddress address, ThreadInfo threadInfo, Position position) : base(threadInfo, position)
        {
            Address = address;
        }
    }
}
