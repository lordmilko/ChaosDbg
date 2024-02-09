using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a raw contiguous collection instructions that have been discovered while attempting to disassemble instructions
    /// that are believed to belong to a function. This region may not contain all instructions that are actually available at a given
    /// location if the disassembler was instructed to ignore instructions that have already been discovered as part of a previously
    /// discovered region.
    /// </summary>
    [DebuggerDisplay("Length = {Length}")]
    public class NativeCodeRegion
    {
        public long StartAddress { get; }

        public long EndAddress
        {
            get
            {
                if (Instructions.Count > 0)
                {
                    var lastInstr = Instructions.Last();

                    //We do -1 because otherwise the end address will be the next address AFTER the end.
                    //i.e. if we occupy bytes 0x1000 and 0x1001, the end address should be 0x1001, not
                    //0x1002
                    return lastInstr.Address + lastInstr.Bytes.Length - 1;
                }
                else
                    return StartAddress;
            }
        }

        public int Length => (int) (EndAddress - StartAddress + 1);

#if DEBUG
        public long PhysicalStartAddress { get; set; }

        public long PhysicalEndAddress { get; set; }
#endif

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IList<INativeInstruction> Instructions { get; }

        internal NativeCodeRegion(long address, IList<INativeInstruction> instructions)
        {
            StartAddress = address;
            Instructions = instructions;
        }

        public bool Contains(long address) =>
            address >= StartAddress && address <= EndAddress;
    }
}
