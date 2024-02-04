using System.Linq;
using ChaosDbg.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class ByteSequenceTests
    {
        #region Byte

        [TestMethod]
        public void ByteSequence_FromSingleByte() =>
            TestCtor("0x10", expectedBytes: new byte[] { 0x10 }, expectedMask: new byte[] { 0xff });

        [TestMethod]
        public void ByteSequence_FromMultipleBytes() =>
            TestCtor("0x1011", expectedBytes: new byte[] { 0x10, 0x11 }, expectedMask: new byte[] { 0xff, 0xff });

        [TestMethod]
        public void ByteSequence_FromHiByte() =>
            TestCtor("0x1.", expectedBytes: new byte[] { 0x10 }, expectedMask: new byte[] { 0xf0 });

        [TestMethod]
        public void ByteSequence_FromLoByte() =>
            TestCtor("0x.1", expectedBytes: new byte[] { 0x01 }, expectedMask: new byte[] { 0x0f });

        [TestMethod]
        public void ByteSequence_FromAnyByte() =>
            TestCtor("0x..", expectedBytes: new byte[] { 0x00 }, expectedMask: new byte[] { 0x00 });

        [TestMethod]
        public void ByteSequence_Byte_ToString()
        {
            var sequence = new ByteSequence("0x1. 0x.1 * 0xffee");

            Assert.AreEqual("0x1. 0x.1 * 0xffee", sequence.ToString());
        }

        #endregion
        #region Binary

        [TestMethod]
        public void ByteSequence_FromBinary() =>
            TestCtor("11111110", expectedBytes: new byte[] { 0xfe }, expectedMask: new byte[] { 0xff });

        [TestMethod]
        public void ByteSequence_FromBinary_TwoBytes()
        {
            var sequence = new ByteSequence("1111111. 1111111.");

            Assert.AreEqual("1111111.1111111.", sequence.ToString());
        }

        [TestMethod]
        public void ByteSequence_FromBinary_CertainBytes() =>
            TestCtor("111110..", expectedBytes: new byte[] { 0xf8 }, expectedMask: new byte[] { 0xfc });

        [TestMethod]
        public void ByteSequence_HexAndBinary() =>
            TestCtor("0x1. 111110..", expectedBytes: new byte[] { 0x10, 0xf8 }, expectedMask: new byte[] { 0xf0, 0xfc });

        [TestMethod]
        public void ByteSequence_Binary_ToString()
        {
            var sequence = new ByteSequence("101110..");

            Assert.AreEqual("101110..", sequence.ToString());
        }

        [TestMethod]
        public void ByteSequence_BinaryAndHex_ToString()
        {
            var sequence = new ByteSequence("0xcc * 0x40 01010... 0x4883ec");

            Assert.AreEqual("0xcc * 0x40 01010... 0x4883ec", sequence.ToString());
        }

        #endregion
        #region HasByte

        [TestMethod]
        public void ByteSequence_HasByte_FullMask()
        {
            var sequence = new ByteSequence("0x10");

            Assert.IsTrue(sequence.HasByte(0, 0x10));
            Assert.IsFalse(sequence.HasByte(0, 0x11));
        }

        [TestMethod]
        public void ByteSequence_HasByte_PartialMask()
        {
            var sequence = new ByteSequence("0x1.");

            Assert.IsTrue(sequence.HasByte(0, 0x10));
            Assert.IsTrue(sequence.HasByte(0, 0x11));
        }

        #endregion
        #region XFG

        [TestMethod]
        public void ByteSequence_Xfg_SingleJunkByte()
        {
            var sequence = new ByteSequence("0xcc * 0x10");

            var xfg = sequence.WithXfg();

            Assert.AreEqual("0xcc 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. * 0x10", xfg.ToString());
        }

        [TestMethod]
        public void ByteSequence_Xfg_RepeatingJunkBytes()
        {
            var sequence = new ByteSequence("0xcccccc * 0x10");

            var xfg = sequence.WithXfg();

            Assert.AreEqual("0xcccccc 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. * 0x10", xfg.ToString());
        }

        [TestMethod]
        public void ByteSequence_Xfg_NonRepeatingJunkBytes()
        {
            var sequence = new ByteSequence("0xcc90 * 0x10");

            var xfg = sequence.WithXfg();

            Assert.AreEqual("0xcc90 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. * 0x10", xfg.ToString());
        }

        [TestMethod]
        public void ByteSequence_Xfg_WithBinaryAndRepeatingBytes()
        {
            var sequence = new ByteSequence("0xcccccc * 0x4889 01...100..100100");

            Assert.AreEqual("0xcccccc * 0x4889 01...100..100100", sequence.ToString());

            var xfg = sequence.WithXfg();

            Assert.AreEqual("0xcccccc 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. 0x.. * 0x4889 01...100..100100", xfg.ToString());
        }

        #endregion

        [TestMethod]
        public void ByteSequence_InstrBytes_ToString()
        {
            var sequence = new ByteSequence(InstrBytes.Int3, "*", InstrBytes.MovAnyRsp);

            Assert.AreEqual("0xcc * 01001.00 0x8b 11...100", sequence.ToString());
        }

        #region Tree

        [TestMethod]
        public void ByteSequenceTree_HasLeaves()
        {
            var s1 = new ByteSequence("0xff01");
            var s2 = new ByteSequence("0xff02");

            var tree = ByteSequenceTreeNode.BuildTree(new[] {s1, s2});

            Assert.AreEqual(s1, tree[0xff][0x01].Complete[0]);
            Assert.AreEqual(s2, tree[0xff][0x02].Complete[0]);
        }

        [TestMethod]
        public void ByteSequenceTree_MatchAll()
        {
            var s1 = new ByteSequence("0xff01");
            var s2 = new ByteSequence("0xff02");

            var tree = ByteSequenceTreeNode.BuildTree(new[] { s1, s2 });

            var bytes = new byte[]
            {
                0xff, 0xfe, 0xff, 0x01,
                0x00, 0xff, 0xff, 0x02
            };

            var matches = tree.GetMatches(bytes).ToArray();

            Assert.AreEqual(2, matches.Length);
            Assert.AreEqual(s1, matches[0].Sequence);
            Assert.AreEqual(s2, matches[1].Sequence);
        }

        #endregion

        [TestMethod]
        public void ByteSequence_WithMark()
        {
            var sequence = new ByteSequence("0x10 * 0x20");

            Assert.AreEqual(1, sequence.Mark);
        }

        private void TestCtor(string input, byte[] expectedBytes, byte[] expectedMask)
        {
            var sequence = new ByteSequence(input);

            AssertEx.ArrayEqual(expectedBytes, sequence.Bytes, "Bytes");
            AssertEx.ArrayEqual(expectedMask, sequence.Masks, "Masks");
        }
    }
}
