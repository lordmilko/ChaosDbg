using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ChaosDbg.Tests
{
    /// <summary>
    /// Provides info about a <see cref="GlyphRunDrawing"/> used to print formatted text.
    /// </summary>
    class GlyphDrawingInfo : DrawingInfo
    {
        public FormattedText Text { get; }

        public Typeface Typeface { get; }

        public double FontSize { get; }

        public Brush Color { get; }

        public Point Origin { get; }

        public GlyphRunDrawing Drawing { get; }

        public GlyphDrawingInfo(GlyphRunDrawing drawing)
        {
            var value = new string(drawing.GlyphRun.Characters.ToArray());

            var glyphRun = drawing.GlyphRun;

            Typeface = new Typeface(glyphRun.GlyphTypeface.FamilyNames[new CultureInfo("en-US")]);
            FontSize = glyphRun.FontRenderingEmSize;
            Color = drawing.ForegroundBrush;

#pragma warning disable CS0618 // Type or member is obsolete
            Text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, glyphRun.FontRenderingEmSize, drawing.ForegroundBrush);
#pragma warning restore CS0618 // Type or member is obsolete

            //The Y coordinate gets offset by a baseline value on FormattedText due to DPI scaling. The X coordinate is not affected
            Origin = new Point(glyphRun.BaselineOrigin.X, glyphRun.BaselineOrigin.Y - Text.Baseline);
            Drawing = drawing;
        }        

        public override string ToString()
        {
            return $"{Text.Text} ({Origin})";
        }
    }
}
