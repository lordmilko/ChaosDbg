using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using ChaosDbg.Analysis;
using ChaosDbg.Cordb;
using ChaosDbg.Disasm;
using ChaosDbg.Text;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    static class VerifyExtensions
    {
        public static void Verify(this NativeDisassembler engine, string defaultFormat, string customFormat)
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

        public static void Verify(this SplitViewInfo info, double dockedWidth, double dockedHeight, Orientation orientation, params Action<IPaneItem[]>[] splitterItemDescendantVerifier)
        {
            Assert.AreEqual(dockedWidth, info.DockedWidth.Value);
            Assert.AreEqual(dockedHeight, info.DockedHeight.Value);
            Assert.AreEqual(orientation, info.Orientation);
        }

        #endregion
        #region Variable

        public static CordbThreadVerifier Verify(this CordbThread thread) => new CordbThreadVerifier(thread);

        public static void Verify(this CordbNativeVariable[] variables, params Action<CordbNativeVariable>[] verifiers)
        {
            Assert.AreEqual(verifiers.Length, variables.Length);

            for (var i = 0; i < variables.Length; i++)
            {
                var verifier = verifiers[i];

                if (verifier == null)
                    continue; //The caller is saying the variable contains junk and not to verify it

                verifier(variables[i]);
            }
        }

        public static void Verify(this CordbNativeVariable variable, string name, object expectedValue)
        {
            Assert.AreEqual(name, variable.Name);
            Assert.IsTrue(variable.Value.IsEquivalentTo(expectedValue));
        }

        public static void Verify(this CordbManagedVariable[] variables, params Action<CordbManagedVariable>[] verifiers)
        {
            Assert.AreEqual(verifiers.Length, variables.Length);

            for (var i = 0; i < variables.Length; i++)
                verifiers[i](variables[i]);
        }

        public static void Verify(this CordbManagedVariable variable, string name, object expectedValue)
        {
            Assert.AreEqual(name, variable.Name);
            Assert.IsTrue(variable.Value.IsEquivalentTo(expectedValue));
        }

        #endregion

        public static void Verify(this ChunkGraph[] chunks, params Action<ChunkGraph>[] verifiers)
        {
            Assert.AreEqual(verifiers.Length, chunks.Length);

            for (var i = 0; i < chunks.Length; i++)
                verifiers[i](chunks[i]);
        }

        public static void Verify(this ChunkGraph graph, params string[] expected)
        {
            Assert.IsTrue(expected.Length == graph.Vertices.Count, $"Expected: {string.Join(", ", expected)}, Actual: {string.Join(", ", graph.Vertices)}");

            for (var i = 0; i < graph.Vertices.Count; i++)
                Assert.AreEqual(expected[i], graph.Vertices[i].ToString());
        }

        public static void Verify(this COR_DEBUG_IL_TO_NATIVE_MAP map, int ilOffset, int nativeStartOffset, int nativeEndOffset)
        {
            Assert.AreEqual(ilOffset, map.ilOffset);
            Assert.AreEqual(nativeStartOffset, map.nativeStartOffset);
            Assert.AreEqual(nativeEndOffset, map.nativeEndOffset);
        }
    }
}
