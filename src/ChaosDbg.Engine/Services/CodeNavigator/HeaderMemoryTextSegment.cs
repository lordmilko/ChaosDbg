using System;
using System.Collections.Generic;
using ChaosDbg.Text;

namespace ChaosDbg
{
    class HeaderMemoryTextSegment : IMemoryTextSegment
    {
        public long Position
        {
            get => lastValidAddress;
            set => lastValidAddress = value;
        }

        public int SegmentStart { get; }

        public int SegmentEnd { get; }

        private long lastValidAddress;

        public HeaderMemoryTextSegment(int headerStart, int headerEnd)
        {
            SegmentStart = headerStart;
            SegmentEnd = headerEnd;

            lastValidAddress = SegmentStart;
        }

        public long StepUp(int count)
        {
            lastValidAddress += -count;
            lastValidAddress = Math.Max(lastValidAddress, SegmentStart);
            return lastValidAddress;
        }

        public long StepDown(int count)
        {
            lastValidAddress += count;
            lastValidAddress = Math.Min(lastValidAddress, SegmentEnd);
            return lastValidAddress;
        }

        public long SeekVertical(long newOffset)
        {
            lastValidAddress = newOffset;

            if (lastValidAddress > SegmentEnd)
                lastValidAddress = SegmentEnd;

            if (lastValidAddress < SegmentStart)
                lastValidAddress = SegmentStart;

            return lastValidAddress;
        }

        public ITextLine[] GetLines(long startRVA, long endRVA)
        {
            var results = new List<ITextLine>();

            for (var i = startRVA; i < endRVA; i++)
            {
                results.Add(new TextLine(new TextRun($"{i} header")));
            }

            return results.ToArray();
        }

        public override string ToString()
        {
            return $"Header ({SegmentStart:X} - {SegmentEnd:X})";
        }
    }
}
