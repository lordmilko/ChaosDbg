using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Disasm;
using ChaosDbg.Metadata;
using ChaosDbg.Text;

namespace ChaosDbg
{
    class CodeMemoryTextSegment : IMemoryTextSegment
    {
        public IImageSectionHeader SectionHeader { get; }

        public long Position
        {
            get => lastValidAddress;
            set => lastValidAddress = value;
        }

        public int SegmentStart { get; }

        public int SegmentEnd { get; }

        private INativeDisassembler nativeDisassembler;
        private long moduleBase;
        private long lastValidAddress;

        public CodeMemoryTextSegment(
            IImageSectionHeader sectionHeader,
            INativeDisassembler nativeDisassembler,
            long moduleBase,
            int start,
            int end)
        {
            SectionHeader = sectionHeader;
            SegmentStart = start;
            SegmentEnd = end;

            this.nativeDisassembler = nativeDisassembler;
            this.moduleBase = moduleBase;
            lastValidAddress = SegmentStart;
        }

        public long StepUp(int count)
        {
            //Get the last valid address before us. We're not scrolling so there's no guess offset
            var result = GetPreviousRVA(lastValidAddress - 1, 0, Math.Abs(count));

            lastValidAddress = result;

            return lastValidAddress;
        }

        public long StepDown(int count)
        {
            var result = nativeDisassembler.Disassemble(lastValidAddress + moduleBase, count);

            if (result.Length > 0)
            {
                var last = result.Last();

                var newRVA = last.IP - moduleBase;
                lastValidAddress = newRVA + last.Bytes.Length;
            }

            return lastValidAddress;
        }

        public long SeekVertical(long newOffset)
        {
            var diff = newOffset - lastValidAddress;

            var result = GetPreviousRVA(lastValidAddress, (int) diff, 1);

            lastValidAddress = result;

            return result;
        }

        public ITextLine[] GetLines(long startRVA, long endRVA)
        {
            var numLines = endRVA - startRVA;

            if (startRVA < lastValidAddress)
            {
                //We want to start before the last address we know is valid. Disassemble back enough so we're at
                //or before the desired starting position

                var bytesToReverse = startRVA - lastValidAddress;

                var adjustedStart = GetPreviousRVA(lastValidAddress, (int) bytesToReverse, 1);

                var instructions = nativeDisassembler.Disassemble(moduleBase + adjustedStart, (int) numLines);

                return instructions.Select(d => (ITextLine)new TextLine(new TextRun(d.ToString()))).ToArray();
            }
            else
            {
                //Our last valid address is before the desired starting position. Disassemble forwards and then disassemble enough
                //instructions to fill the desired number of lines

                var instructions = new List<INativeInstruction>();

                var ip = lastValidAddress;

                while (instructions.Count < numLines)
                {
                    var instr = nativeDisassembler.Disassemble(moduleBase + ip);

                    if (instr == null)
                        break;

                    if (ip >= startRVA)
                        instructions.Add(instr);

                    ip += instr.Bytes.Length;
                }

                return instructions.Select(d => (ITextLine) new TextLine(new TextRun(d.ToString()))).ToArray();
            }
        }

        private long GetPreviousRVA(long currentRVA, int guessOffset, int count)
        {
            /* We wish to locate the nearest valid address negativeOffset bytes behind currentRVA.
             * We can't simply do currentRVA - negativeOffset however, because x86 uses variable length instructions,
             * and we don't know whether we'll end up halfway through a given instruction or not. Some tricky workarounds
             * you can potentially employ on x86 are to look for the nearest valid symbol prior to the current address (and
             * then disassemble forwards from there, which is the approach preferred by WinDbg) or alternatively you can jump
             * back a small range and try and disassemble a number of instructions to try in the hope we might trip over some
             * garbage which we can use to re-adjust the offset of where we think the next valid instruction is (the approach
             * preferred by OllyDbg/x64dbg) */

            //The approximate memory address we're aiming for. We would like the closest memory address up to and including - but not after - this address
            var targetRVA = currentRVA + guessOffset;

            //The RVA to start searching for valid instructions from. The maximum instruction size on x86 is 16 bytes, so I think we're
            //going to try and inspect at least 4 instructions
            var startRVA = targetRVA - 16 * (count + 3);

            //If the start RVA is below the start of the segment, bring it up to the start of the segment
            startRVA = Math.Max(SegmentStart, startRVA);

            //if (startRVA - segmentStart < count)
            //    return startRVA;

            if (targetRVA < startRVA)
                return startRVA;

            //After calculating the RVA we're going to start searching from, get the number bytes in the search range. This will be at least 4 instructions worth
            //unless our targetRVA is very close to the start of the segment, in which case it'll be less
            var searchRange = targetRVA - startRVA;

            var startAddr = moduleBase + startRVA;
            var currentOffset = 0;

            var instructions = new List<INativeInstruction>();

            while (currentOffset <= searchRange)
            {
                var instr = nativeDisassembler.Disassemble(startAddr + currentOffset);

                if (instr == null)
                {
                    //Instructions can sometimes start with FF FE or FF FF. Maybe we had these bytes
                    currentOffset += 2;
                }
                else
                {
                    instructions.Add(instr);
                    currentOffset += instr.Bytes.Length;
                }
            }

            INativeInstruction last;

            if (instructions.Count == 1)
                return instructions[0].IP - moduleBase;

            if (count != 1)
                last = instructions[instructions.Count - count - 1];
            else
                last = instructions.Last();

            var endAddress = last.IP + last.Bytes.Length;
            var endRVA = endAddress - moduleBase;

            if (endRVA > targetRVA)
                return last.IP - moduleBase;

            return endRVA;
        }

        public override string ToString()
        {
            return $"{SectionHeader} ({SegmentStart:X} - {SegmentEnd:X})";
        }
    }
}
