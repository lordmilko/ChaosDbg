using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    //Unit tests for validating that we can properly detect things which happen on a canvas

    [TestClass]
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
        public void Canvas_Scroll()
        {
            AppRunner.WithInProcessApp(_ =>
            {
                AppRunner.Invoke(a =>
                {
                    var group = a.MainWindow.GetDrawingGroup<TextCanvas>();

                    var verifiers = new List<Action<DrawingInfo>>();

                    for (var i = 0; i < 50; i++)
                    {
                        var i1 = i;
                        verifiers.Add(v => v.HasText(i1.ToString(), 14, "Consolas", Brushes.Black, 0, 0));
                    }

                    group.Verify(verifiers.ToArray());
                });
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
