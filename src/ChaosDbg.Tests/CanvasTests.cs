using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ChaosDbg.Scroll;
using ChaosDbg.Text;
using ChaosDbg.Theme;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    //Unit tests for validating that we can properly detect things which happen on a canvas

    [TestClass]
    [DoNotParallelize]
    public class CanvasTests
    {
        [TestMethod]
        public void Canvas_ReadString()
        {
            //Test reading a string from an arbitrary X/Y coordinate with a custom colour

            Test(
                d => d.DrawText(MakeText("foo", 14, "Consolas", Brushes.Green), new Point(10, 10)),
                g => g.Verify(
                    e => e.HasText("foo", 14, "Consolas", Brushes.Green, 10, 10)
                )
            );
        }

        [TestMethod]
        public void Canvas_Scroll_LineDown()
        {
            TestScroll(
                0, 27,
                m =>
                {
                    //After point to pixels on a single line down, we're at 15.2 but a single line is 15.8, so we do another line down to get us over
                    //the threshold for the purposes of the test
                    m.LineDown();
                    m.LineDown();
                },
                1, 29
            );
        }

        [TestMethod]
        public void Canvas_Selection_Click_FirstRow()
        {
            TestSelection(b => b
                .Click("Th", after: "e") //3
                .Expect(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty)
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_FirstRow()
        {
            TestSelection(b => b
                .Down("Th", after: "e") //3
                .Move("The quic", "k") //9
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 9), text: " quick")
                )
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_BeforeEnd_FirstRow()
        {
            TestSelection(b => b
                .Down("Th", after: "e") //3
                .Move("The quick brown f", after: "o")
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 18), text: " quick brown fo")
                )
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_AfterEnd_FirstRow()
        {
            TestSelection(b => b
                .Down("Th", after: "e") //3
                .Move("The quick brown fo", "x")
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 19), text: " quick brown fox")
                )
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_FarBeyondEnd_FirstRow()
        {
            TestSelection(b => b
                .Down("Th", after: "e") //3
                .Move("The quick brown fox", "      ")
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 19), text: " quick brown fox")
                )
            );
        }

        [TestMethod]
        public void Canvas_Selection_Click_SecondRow()
        {
            TestSelection(b => b
                .Click("ju", after: "m", rowOffset: 1) //3
                .Expect(start: new TextPosition(1, 3), end: new TextPosition(1, 3), string.Empty)
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_SecondRow()
        {
            TestSelection(b => b
                .Down("ju", after: "m", 1) //3
                .Move("jumps ove", after: "r") //10
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(1, 3), end: new TextPosition(1, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(1, 3), end: new TextPosition(1, 10), text: "ps over")
                )
            );
        }

        [TestMethod]
        public void Canvas_Selection_ClickDrag_MultipleRows()
        {
            TestSelection(b => b
                .Down("Th", after: "e")
                .Move("jum", after: "p", 1)
                .Up()
                .Expect(
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(0, 3), text: string.Empty),
                    e => e.Verify(start: new TextPosition(0, 3), end: new TextPosition(1, 4), text: $" quick brown fox{Environment.NewLine}jump")
                )
            );
        }

        private void TestScroll(
            int initialStartIndex, int initialEndIndex,
            Action<ScrollManager> action,
            int finalStartIndex, int finalEndIndex)
        {
            var lines = Enumerable.Range(0, 60).Select(v => (ITextLine) new TextLine(new TextRun(v.ToString()))).ToArray();

            AppRunner.WithInProcessApp(CreateTestWindow(lines), w =>
            {
                var group = w.GetDrawingGroup<TextCanvas>();

                var verifiers = new List<Action<DrawingInfo>>();

                for (var i = initialStartIndex; i < initialEndIndex; i++)
                {
                    var i1 = i;
                    verifiers.Add(v => v.HasText(i1.ToString(), 13.5, "Consolas", Brushes.Black, 0, 0));
                }

                group.Verify(verifiers.ToArray());

                var canvas = w.GetLogicalDescendant<TextCanvas>();

                action(canvas.ScrollManager);

                var op = Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    verifiers = new List<Action<DrawingInfo>>();

                    for (var i = finalStartIndex; i < finalEndIndex; i++)
                    {
                        var i1 = i;
                        verifiers.Add(v => v.HasText(i1.ToString(), 13.5, "Consolas", Brushes.Black, 0, 0));
                    }

                    group.Verify(verifiers.ToArray());
                }), DispatcherPriority.ContextIdle, null);

                canvas.ScrollManager.ForceInvalidateScrolledArea();

                op.Wait();
            });
        }

        private void TestSelection(Action<SelectionBuilder> action)
        {
            var lines = new ITextLine[]
            {
                new TextLine(new TextRun("The quick brown fox")),
                new TextLine(new TextRun("jumps over the lazy dog"))
            };

            AppRunner.WithInProcessApp(CreateTestWindow(lines), w =>
            {
                var hwnd = new WindowInteropHelper(w).Handle;

                using (var automation = new UIA3Automation())
                {
                    var flaWindow = automation.FromHandle(hwnd).AsWindow();

                    var canvas = w.GetVisualDescendant<TextCanvas>();

                    var cf = new ConditionFactory(new UIA3PropertyLibrary());

                    //I think this is referencing the x:Name from the TextPaneControl
                    var ctrl = flaWindow.FindFirstDescendant(cf.ByAutomationId("Canvas"));

                    var builder = new SelectionBuilder(canvas, ctrl);

                    action(builder);
                }
            });
        }

        private Func<TestWindow> CreateTestWindow(ITextLine[] lines)
        {
            Func<TestWindow> createWindow = () =>
            {
                var window = new TestWindow();

                var pane = new TextPaneControl();

                //UiTextLine will get its font from the current ITheme, so we can't make up some random font to use for the test
                var font = GlobalProvider.ServiceProvider.GetService<IThemeProvider>().GetTheme().ContentFont;

                pane.RawContent = new TextBuffer(font, lines);
                window.Content = pane;

                return window;
            };

            return createWindow;
        }

        private FormattedText MakeText(
            string text,
            double size,
            string typefaceName,
            Brush color)
        {
            var typeface = new Typeface(typefaceName);

#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                color
            );
#pragma warning restore CS0618 // Type or member is obsolete

            return formattedText;
        }

        private void Test(Action<DrawingContext> action, Action<DrawingGroup> verify)
        {
            var drawingGroup = new DrawingGroup();

            using (var drawingContext = drawingGroup.Open())
                action(drawingContext);

            verify(drawingGroup);
        }
    }
}
