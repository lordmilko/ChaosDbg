using System.Collections.Generic;
using ChaosDbg.Disasm;

namespace ChaosDbg.Tests
{
    class MockInstruction : IInstruction
    {
        public string Name { get; }
        public long Address { get; }

        private List<MockInstruction> jumps = new List<MockInstruction>();

        public void AddJumps(params MockInstruction[] instrs)
        {
            jumps.AddRange(instrs);
        }

        public bool IsRet { get; set; }

        public bool JumpsTo(MockInstruction instr) => jumps.Contains(instr);

        public MockInstruction(string name, long address)
        {
            Name = name;
            Address = address;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
