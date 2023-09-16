using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ChaosDbg.Scroll;
using ChaosDbg.Text;
using ChaosDbg.Theme;
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

        private void TestScroll(
            int initialStartIndex, int initialEndIndex,
            Action<ScrollManager> action,
            int finalStartIndex, int finalEndIndex)
        {
            Func<TestWindow> createWindow = () =>
            {
                var window = new TestWindow();

                var pane = new TextPaneControl();
                var lines = Enumerable.Range(0, 60).Select(v => (ITextLine) new TextLine(new TextRun(v.ToString()))).ToArray();

                //UiTextLine will get its font from the current ITheme, so we can't make up some random font to use for the test
                var font = GlobalProvider.ServiceProvider.GetService<IThemeProvider>().GetTheme().ContentFont;

                pane.RawContent = new TextBuffer(font, lines);
                window.Content = pane;

                return window;
            };

            AppRunner.WithInProcessApp(createWindow, w =>
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
