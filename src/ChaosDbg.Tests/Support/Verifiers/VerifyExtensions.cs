using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using ChaosDbg.Disasm;
using ChaosDbg.Text;
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

            var infos = desc.Select(DrawingInfo.New).ToArray();

            for (var i = 0; i < infos.Length; i++)
                verifiers[i](infos[i]);
        }

        public static MemoryTextSegmentVerifier Verify(this IMemoryTextSegment segment) => new MemoryTextSegmentVerifier(segment);

        public static CodeNavigatorVerifier Verify(this CodeNavigator nav) => new CodeNavigatorVerifier(nav);

        public static void Verify(this SelectionChangedEventArgs e, TextPosition start, TextPosition end, string text)
        {
            Assert.AreEqual(start, e.SelectedRange.Start);
            Assert.AreEqual(end, e.SelectedRange.End);
            Assert.AreEqual(text, e.SelectedText);
        }

        #region Pane

        public static void Verify(this SplitterItemsDockContainerInfo info, double dockedWidth, double dockedHeight, Orientation orientation, params Action<IPaneItem[]>[] splitterItemDescendantVerifier)
        {
            Assert.AreEqual(dockedWidth, info.DockedWidth.Value);
            Assert.AreEqual(dockedHeight, info.DockedHeight.Value);
            Assert.AreEqual(orientation, info.Orientation);
        }

        #endregion
    }
}
