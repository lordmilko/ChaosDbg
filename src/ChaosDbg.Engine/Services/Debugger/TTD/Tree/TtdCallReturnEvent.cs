using System;
using System.Diagnostics;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    [DebuggerDisplay("[{Position.ToString(),nq}] Name = {Name}, FunctionAddress = {FunctionAddress}, ReturnAddress = {ReturnAddress}")]
    class TtdCallReturnEvent : TtdCallTreeEvent
    {
        public GuestAddress FunctionAddress { get; }
        public IntPtr ReturnAddress { get; }

        public TtdCallReturnEvent(GuestAddress functionAddress, IntPtr returnAddress, ThreadInfo threadInfo, Position position) : base(threadInfo, position)
        {
            FunctionAddress = functionAddress;
            ReturnAddress = returnAddress;
        }
    }
}
