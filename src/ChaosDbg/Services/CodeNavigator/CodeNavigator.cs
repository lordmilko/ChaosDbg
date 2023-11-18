using System;
using System.Collections.Generic;
using ChaosDbg.Disasm;
using ChaosDbg.Text;
using ChaosLib.Metadata;

namespace ChaosDbg
{
    class CodeNavigator
    {
        public IMemoryTextSegment[] Segments { get; }

        private IPEFile pe;
        private ulong BaseAddress => pe.OptionalHeader.ImageBase;
        private int currentSegmentIndex = 0;
        private IMemoryTextSegment currentSegment => Segments[currentSegmentIndex];

        public CodeNavigator(IPEFile pe, INativeDisassembler nativeDisassembler)
        {
            this.pe = pe;

            var headerStart = 0;
            var headerEnd = pe.OptionalHeader.BaseOfCode - 1;

            var segmentsList = new List<IMemoryTextSegment>();

            segmentsList.Add(new HeaderMemoryTextSegment(headerStart, headerEnd));

            for (var i = 0; i < pe.SectionHeaders.Length; i++)
            {
                var start = pe.SectionHeaders[i].VirtualAddress;
                int end;

                if (i < pe.SectionHeaders.Length - 1)
                    end = pe.SectionHeaders[i + 1].VirtualAddress - 1;
                else
                    end = pe.OptionalHeader.SizeOfImage;

                segmentsList.Add(new CodeMemoryTextSegment(
                    pe.SectionHeaders[i],
                    nativeDisassembler,
                    (long)BaseAddress,
                    start,
                    end
                ));
            }

            Segments = segmentsList.ToArray();
        }

        public int StepUp(int count)
        {
            count = Math.Abs(count);

            if (currentSegment.Position - (count * 16) > currentSegment.SegmentStart)
            {
                var result = currentSegment.StepUp(count);

                if (result <= currentSegment.SegmentStart)
                    throw new InvalidOperationException($"Expected step up result {result:X} to be after segment {currentSegment} start {currentSegment.SegmentStart:X}");

                return (int) result;
            }
            else
            {
                //We're close to the start of the segment; step one line at a time

                long result = 0;

                for (var i = 0; i < count; i++)
                {
                    var initial = currentSegment.Position;

                    result = currentSegment.StepUp(1);

                    if (initial == result)
                    {
                        if (currentSegmentIndex > 0)
                        {
                            currentSegmentIndex--;

                            if (currentSegment is CodeMemoryTextSegment)
                                throw new NotImplementedException("Don't know what the first position should be at the end of the segment for a code segment");

                            currentSegment.Position = currentSegment.SegmentEnd;
                            result = currentSegment.SegmentEnd;
                        }
                    }
                }

                return (int) result;
            }
        }

        public int StepDown(int count)
        {
            if (currentSegment.Position + (count * 16) < currentSegment.SegmentEnd)
            {
                //The maximum size of one instruction is 16 bytes. So worse case scenario, we need to skip count * 16 instructions for a single line.
                //If, after doing that, we're still below the end of the segment, we can take a fast path by just doing the entire step in one go
                var result = currentSegment.StepDown(count);

                if (result >= currentSegment.SegmentEnd)
                    throw new InvalidOperationException($"Expected step down result {result:X} to be prior to segment {currentSegment} end {currentSegment.SegmentEnd:X}");

                return (int) result;
            }
            else
            {
                //We're close to the end of the segment; step one line at a time

                long result = 0;

                for (var i = 0; i < count; i++)
                {
                    var initial = currentSegment.Position;

                    result = currentSegment.StepDown(1);

                    //No need to modify the current index; implicitly, moving to a new segment means we've moved to the new segments start address
                    if (initial == result)
                    {
                        if (currentSegmentIndex < Segments.Length - 1)
                        {
                            currentSegmentIndex++;
                            result = currentSegment.Position;
                        }
                    }
                }

                return (int) result;
            }
        }

        public long SeekVertical(long newOffset)
        {
            long result;

            if (newOffset > currentSegment.SegmentStart)
            {
                while (newOffset > currentSegment.SegmentEnd)
                    currentSegmentIndex++;

                result = currentSegment.SeekVertical(newOffset);

                for (var i = 0; i < currentSegmentIndex; i++)
                    Segments[i].Position = Segments[i].SegmentStart;

                //No need to move all segments before us to their ends; if they already are near
                //their ends, great. Otherwise, we can't reliably find the last code instruction
                //in a segment
            }
            else
            {
                //It's a previous segment

                while (newOffset < currentSegment.SegmentStart)
                    currentSegmentIndex--;

                result = currentSegment.SeekVertical(newOffset);

                //We've gone up, so move all segments after us back to their beginnings
                for (var i = currentSegmentIndex + 1; i < Segments.Length; i++)
                    Segments[i].Position = Segments[i].SegmentStart;
            }

            return result;
        }

        public ITextLine[] GetLines(int startRVA, int endRVA)
        {
            var lines = new List<ITextLine>();

            var maxCount = endRVA - startRVA;

            foreach (var segment in Segments)
            {
                var start = Math.Max(segment.SegmentStart, startRVA);
                var end = Math.Min(segment.SegmentEnd, endRVA);

                var count = end - start;

                if (end == segment.SegmentEnd)
                    count++;

                if (count > 0)
                {
                    lines.AddRange(segment.GetLines(start, start + count));
                }

                if (lines.Count >= maxCount)
                    break;
            }

            return lines.ToArray();
        }
    }
}
