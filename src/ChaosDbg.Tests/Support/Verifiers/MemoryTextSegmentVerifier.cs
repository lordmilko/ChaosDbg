using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    struct MemoryTextSegmentVerifier
    {
        private IMemoryTextSegment segment;

        public MemoryTextSegmentVerifier(IMemoryTextSegment segment)
        {
            this.segment = segment;
        }

        public MemoryTextSegmentVerifier StepUp(long expected)
        {
            var actual = segment.StepUp(1);
            Assert.AreEqual(expected, actual);

            return this;
        }

        public MemoryTextSegmentVerifier StepDown(long expected)
        {
            var actual = segment.StepDown(1);
            Assert.AreEqual(expected, actual);

            return this;
        }

        public MemoryTextSegmentVerifier GetLines(long startRVA, long endRVA, string firstAddr, string lastAddr)
        {
            var result = segment.GetLines(startRVA, endRVA);

            var count = endRVA - startRVA;

            Assert.AreEqual(count, result.Length);

            var first = result[0].ToString();
            first = first.Substring(0, first.IndexOf(' '));
            var last = result[result.Length - 1].ToString();
            last = last.Substring(0, last.IndexOf(' '));

            Assert.AreEqual(firstAddr, first);
            Assert.AreEqual(lastAddr, last);

            return this;
        }
    }
}
