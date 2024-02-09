using System;
using System.Collections.Generic;
using System.IO;
using ChaosDbg.Disasm;
using ChaosLib.PortableExecutable;
using Iced.Intel;

namespace ChaosDbg.Analysis
{
    class JumpTableParser
    {
        //RtlMapSecurityErrorToNtStatus has the base address in the same region as the jump table instructions

        //RtlpUnwindPrologue has the base address in a prior region

        //This is shared with the disassembler
        private Stream processStream;
        private long moduleAddress;

        internal JumpTableParser(Stream processStream, long moduleAddress)
        {
            //The processStream is in fact using our MemoryStream internally. We simply need a stream that we can set absolute addresses on
            this.processStream = processStream;
            this.moduleAddress = moduleAddress;
        }

        public bool TryReadJumpTable(
            long functionAddr,
            NativeCodeRegionDisassembler.RegionPathEnumerator enumerator,
            out JumpTableMetadataRange table)
        {
            /* Generally speaking, a jump table should have the following form:
             *
             *   lea     eax, [rcx+7FF6FD00h] ; switch 18 cases
             *   cmp     eax, 17
             *   ja      def_1800F17F0   ; jumptable 00000001800F17F0 default case
             *
             *   lea     r8, cs:180000000h
             *   cdqe
             *
             *   mov     ecx, [r8+rax*4+0F1874h]
             *   add     rcx, r8
             *
             *   jmp     rcx             ; switch jump
             *
             * The essential step of a jump table is the mov
             *   mov ecx,[r8+rax*4+0F1874h]
             *
             * In the mov, several pieces of information are specified:
             *   r8      - the base address that the jump should be performed from. This is usually the base address of the module
             *   rax     - the variable value that contains the index that should be accessed in the jump table
             *   4       - the size of each entry in the jump table
             *   0xF1874 - the RVA in the file at which the jump table slots reside
             *
             * Typically, the jump table will store a series of RVAs identifying the possible targets of the jump table. These RVAs will
             * then be combined with the module base to get the final target that should be jumped to. If an assembly obfuscates things however
             * this can make it much more difficult to figure out what is going on. Ostensibly you would need to execute an emulator to figure out
             * what is ultimately going on. For our purposes, we merely execute a series of basic heuristics to identify jump tables that follow
             * basic patterns.
             *
             * In order to interpret the jump table, we need to do three things
             *
             * 1. Locate the base address that was used in the mov instruction
             * 2. Locate the number of entries stored in the jump table
             * 3. Calculate what to do with the value stored in the jump table to get the target that ultimately gets jumped to
             */

            table = default;

            /* Rewind through the instructions of the current region and try and find an instruction like mov ecx,[r8+rax*4+0F1874h]
             * The base address and number of entries that exist in the jump table will be found further back from this instruction,
             * and all instructions that then operate on the value retrieved from the jump table will be found after it */
            if (!TryGetLastMov(enumerator.RootInstructions, out var movIndex))
                return false;

            //The movInstr implicitly must be in the first region
            var movInstr = enumerator.RootInstructions[movIndex].Instruction;

            /* Rewind through the instructions prior to the mov instruction to try and find the point at which the memory base (r8 in the example above)
             * is set. This may result in inspecting other regions that lead to the current region */
            if (!TryGetMemoryBase(movIndex, movInstr.MemoryBase, enumerator, out var memoryBase))
                return false;

            /* Regardless of what cases you may have in a switch statement, a jump table only works with indices that go one after the other.
             * If a value would be outside of this range, a check is first performed to see whether the value is higher than the highest possible
             * index. If so, we skip straight to the default case. Thus, we attempt to locate this cmp instruction to get the total number of cases
             * that are contained in the jump table. This instruction can occur either before or after the memory base is declared */
            if (!TryGetNumCases(movIndex, enumerator, out var numCases))
                return false;

            var originalPosition = processStream.Position;

            try
            {
                var tableAddr = moduleAddress + (long) movInstr.MemoryDisplacement64;

                processStream.Position = tableAddr;

                //Read what will typically be the raw RVAs from the jump table
                var rawSlotValues = ReadRawSlotValues(numCases, movInstr.MemoryIndexScale);

                //Emulate the instructions that get executed to calculate the jumps
                var jumpTableTargets = EmulateJumps(
                    movInstr,
                    memoryBase,
                    rawSlotValues,
                    movIndex,
                    enumerator
                );

                table = new JumpTableMetadataRange(functionAddr, tableAddr, movInstr.MemoryIndexScale, rawSlotValues, jumpTableTargets);

                return true;
            }
            finally
            {
                processStream.Position = originalPosition;
            }
        }

        private bool TryGetLastMov(List<INativeInstruction> instrs, out int movIndex)
        {
            //-1 is the jmp, so start at -2
            for (var i = instrs.Count - 2; i >= 0; i--)
            {
                var instr = instrs[i];

                if (instr.Instruction.Mnemonic == Mnemonic.Mov)
                {
                    movIndex = i;

                    //When we have a jump table, the MemoryIndex will store the register that contains the slot of the jump table to use.
                    //If we don't have a MemoryIndex, it's just a normal mov instruction
                    if (instr.Instruction.MemoryIndex == Register.None)
                        return false;

                    return true;
                }
            }

            movIndex = default;
            return false;
        }

        private bool TryGetMemoryBase(
            int startIndex,
            Register targetBaseRegister,
            NativeCodeRegionDisassembler.RegionPathEnumerator enumerator,
            out long memoryBase)
        {
            memoryBase = default;
            enumerator.Reset();

            bool? TryGetValueInternal(Instruction instr, out long memoryBase)
            {
                memoryBase = default;

                if (instr.OpCount > 1 && instr.Op0Kind == OpKind.Register && instr.Op0Register == targetBaseRegister)
                {
                    switch (instr.Mnemonic)
                    {
                        case Mnemonic.Lea:
                            switch (instr.Op1Kind)
                            {
                                case OpKind.Memory:
                                    memoryBase = (long) instr.MemoryDisplacement64;
                                    return true;

                                default:
                                    throw new NotImplementedException($"Don't know how to get memory base from operand of type {instr.Op1Kind}");
                            }
                    }

                    return false;
                }

                return null;
            }

            for (var i = startIndex - 1; i >= 0; i--)
            {
                var instr = enumerator.Current.Instrs[i].Instruction;

                var result = TryGetValueInternal(instr, out memoryBase);

                if (result == null)
                    continue;

                return result.Value;
            }

            while (enumerator.MoveNext())
            {
                for (var i = enumerator.Current.JumpIndex - 1; i >= 0; i--)
                {
                    var instr = enumerator.Current.Instrs[i].Instruction;

                    var result = TryGetValueInternal(instr, out memoryBase);

                    if (result == null)
                        continue;

                    return result.Value;
                }
            }

            return false;
        }

        private bool TryGetNumCases(int startIndex, NativeCodeRegionDisassembler.RegionPathEnumerator enumerator, out long numCases)
        {
            //Go until 1 because we also need to check the prior instruction
            for (var i = startIndex - 1; i >= 1; i--)
            {
                var instr = enumerator.RootInstructions[i];

                if (instr.IsJump())
                {
                    if (instr.Instruction.Mnemonic != Mnemonic.Ja)
                        throw new NotImplementedException($"Don't know how to get number of cases from jump of type {instr.Instruction.Mnemonic}");

                    //We're looking for a cmp followed by a ja. We've found the ja, is it preceded by the jmp?

                    var prevInstr = enumerator.RootInstructions[i - 1].Instruction;

                    if (prevInstr.Mnemonic != Mnemonic.Cmp || prevInstr.Op0Kind != OpKind.Register)
                        throw new NotImplementedException($"Don't know how to get number of cases when previous instruction is of type {prevInstr.Mnemonic}");

                    //The index is 0 based, so do +1 to get the total count
                    numCases = (long) prevInstr.GetImmediate(1) + 1;
                    return true;
                }
            }

            numCases = default;
            return false;
        }

        private long[] ReadRawSlotValues(long numCases, int slotSize)
        {
            var rawSlotValues = new long[numCases];

            var reader = new PEBinaryReader(processStream);

            for (var i = 0; i < numCases; i++)
            {
                switch (slotSize)
                {
                    case 4:
                        rawSlotValues[i] = reader.ReadInt32();
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to read value from slot of size {slotSize}");
                }
            }

            return rawSlotValues;
        }

        private long[] EmulateJumps(
            Instruction movInstr,
            long memoryBase,
            long[] rawSlotValues,
            int startInstr,
            NativeCodeRegionDisassembler.RegionPathEnumerator enumerator)
        {
            var results = new long[rawSlotValues.Length];

            for (var i = 0; i < rawSlotValues.Length; i++)
            {
                if (i == 0)
                {
                    //Currently we only support simply add operations

                    //Ignore the mov and final jmp
                    var diff = enumerator.RootInstructions.Count - 2 - startInstr;

                    if (diff != 1)
                        throw new NotImplementedException($"Don't know how to handle having {diff} instructions involved in calculating the jump target"); //todo

                    //Get the instr after the mov
                    var instr = enumerator.RootInstructions[startInstr + 1].Instruction;

                    if (instr.Mnemonic != Mnemonic.Add)
                        throw new NotImplementedException($"Don't know how to calculate a jump target using an instruction of type {instr.Mnemonic}");

                    if (instr.Op0Kind != OpKind.Register || instr.Op0Register != movInstr.Op0Register.GetFullRegister())
                        throw new NotImplementedException($"Don't know how to calculate a jump target with {instr.Op0Kind} mov operand with value {instr.Op0Register}");

                    if (instr.Op1Kind != OpKind.Register || instr.Op1Register != movInstr.MemoryBase)
                        throw new NotImplementedException($"Don't know how to calculate a jump target with a {instr.Op1Kind} base operand with value {instr.Op1Register}");
                }

                results[i] = rawSlotValues[i] + memoryBase;
            }

            return results;
        }
    }
}
