using System;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    internal class TtdMemoryTraceResult<T>
    {
        public DateTime? DateTime { get; set; }

        public GuestAddress Address { get; }

        public Position Position { get; }

        public ThreadInfo Thread { get; }

        public BP_FLAGS BreakpointType { get; }

        public T Context { get; }

        public TtdMemoryTraceResult(DateTime? dateTime, GuestAddress address, Position position, ThreadInfo thread, BP_FLAGS bpType, T context)
        {
            DateTime = dateTime;
            Address = address;
            Position = position;
            Thread = thread;
            BreakpointType = bpType;
            Context = context;
        }
    }
}
