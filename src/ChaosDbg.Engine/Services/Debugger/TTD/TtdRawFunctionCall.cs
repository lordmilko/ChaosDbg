using System;
using System.Diagnostics;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    [DebuggerDisplay("[{StartPosition.ToString(),nq}-{EndPosition.ToString(),nq}] {Name,nq}")]
    public class TtdRawFunctionCall
    {
        public string Name { get; }

        public GuestAddress Address { get; }

        public Position StartPosition { get; }

        public Position EndPosition { get; internal set; }

        public DateTime StartDateTime { get; }

        public DateTime EndDateTime { get; internal set; }

        public GuestAddress ReturnAddress { get; }

        public ThreadId ThreadId { get; }

        public UniqueThreadId UniqueThreadId { get; }

        public TtdRawFunctionCall(string name, GuestAddress address, Position startPosition, DateTime startDateTime, GuestAddress returnAddress, ThreadId threadId, UniqueThreadId uniqueThreadId)
        {
            Name = name;
            Address = address;
            StartPosition = startPosition;
            StartDateTime = startDateTime;
            ReturnAddress = returnAddress;
            ThreadId = threadId;
            UniqueThreadId = uniqueThreadId;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
