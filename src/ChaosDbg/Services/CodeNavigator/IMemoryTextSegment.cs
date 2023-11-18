using ChaosDbg.Text;

namespace ChaosDbg
{
    interface IMemoryTextSegment
    {
        long Position { get; set; }

        int SegmentStart { get; }

        int SegmentEnd { get; }

        long StepUp(int count);

        long StepDown(int count);

        long SeekVertical(long newOffset);

        ITextLine[] GetLines(long startRVA, long endRVA);
    }
}
