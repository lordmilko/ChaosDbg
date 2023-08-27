using System;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    abstract class DrawingInfo
    {
        public static DrawingInfo New(Drawing drawing)
        {
            if (drawing is GlyphRunDrawing g)
                return new GlyphDrawingInfo(g);

            if (drawing is GeometryDrawing geo)
            {
                if (geo.Geometry is LineGeometry)
                    return new LineDrawingInfo(geo);

                throw new NotImplementedException($"Don't know how to handle a {nameof(GeometryDrawing)} containing geometry of type {geo.Geometry.GetType().Name}");
            }

            throw new NotImplementedException($"Don't know how to create {nameof(DrawingInfo)} for drawing of type {drawing.GetType().Name}");
        }

        public void HasText(
            string text,
            double size,
            string typefaceName,
            Brush color,
            double x,
            double y)
        {
            var info = As<GlyphDrawingInfo>();

            Assert.AreEqual(text, info.Text.Text);
            Assert.AreEqual(size, info.FontSize);
            Assert.AreEqual(typefaceName, info.Typeface.FontFamily.Source);
            Assert.AreEqual(color, info.Color);

            //The numbers are equal as long as x - y <= delta.
            //We want to ensure at least 2 decimal places accuracy, so I think
            //this is the way you'd do that
            Assert.AreEqual(x, info.Origin.X, 0.009);
            Assert.AreEqual(y, info.Origin.Y, 0.009);
        }

        private T As<T>() where T : DrawingInfo
        {
            if (!(this is T))
                Assert.Fail("Got {} of type {} instead of type {}");

            return (T) this;
        }
    }
}
