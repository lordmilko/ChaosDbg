using System.Diagnostics;
using ChaosDbg.Disasm;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    enum TtdDataFlowTag
    {
        /// <summary>
        /// Indicates that this <see cref="TtdDataFlowItem"/> pertains to tracing the target value we're interested in finding the origin of.
        /// </summary>
        ValueOrigin,

        /// <summary>
        /// Indicates that this <see cref="TtdDataFlowItem"/> pertains to tracing the pointer to a buffer that then contained the target value
        /// we're interested in finding the origin of.
        /// </summary>
        PointerOrigin
    }

    [DebuggerDisplay("[{Position.ToString(),nq}] {Name} : {Instruction}")]
    class TtdDataFlowItem
    {
        public GuestAddress Target { get; }

        public ThreadInfo Thread { get; }

        public string Name { get; }

        public Position Position { get; }

        public INativeInstruction Instruction { get; }

        public TtdDataFlowTag Tag { get; internal set; }

        /// <summary>
        /// Gets the location of the target value (i.e. either a register or a memory address)
        /// </summary>
        public object Location { get; internal set; }

        public TtdDataFlowItem(GuestAddress target, string name, ThreadInfo thread, Position position, INativeInstruction instruction)
        {
            Target = target;
            Name = name;
            Thread = thread;
            Position = position;
            Instruction = instruction;
        }
    }
}
