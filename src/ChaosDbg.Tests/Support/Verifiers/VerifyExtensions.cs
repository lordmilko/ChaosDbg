using System;
using System.Linq;
using System.Windows.Media;
using ChaosDbg.Disasm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    static class VerifyExtensions
    {
        public static void Verify(this INativeDisassembler engine, string defaultFormat, string customFormat)
        {
            var result = engine.Disassemble(0, 1);

            Assert.AreEqual(1, result.Length, "Expected number of disassembled instructions was not correct");

            Assert.AreEqual(defaultFormat, result[0].Instruction.ToString(), "Default instruction format was not correct");
            Assert.AreEqual(customFormat, engine.Format(result[0]), "Custom instruction format was not correct");
        }

        public static void Verify(this DrawingGroup drawingGroup, params Action<DrawingInfo>[] verifiers)
        {
            var desc = drawingGroup.DescendantDrawings().ToArray();

            Assert.AreEqual(verifiers.Length, desc.Length, "Number of drawings in drawing group did not match number of verifiers");

            for (var i = 0; i < desc.Length; i++)
                verifiers[i](DrawingInfo.New(desc[i]));
        }

        public static MemoryTextSegmentVerifier Verify(this IMemoryTextSegment segment) => new MemoryTextSegmentVerifier(segment);

        public static CodeNavigatorVerifier Verify(this CodeNavigator nav) => new CodeNavigatorVerifier(nav);
    }
}
