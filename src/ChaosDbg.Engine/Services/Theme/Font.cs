using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ChaosDbg.Theme
{
    public class Font
    {
        public FontFamily FontFamily { get; }

        public Typeface Typeface { get; }

        public double FontSize { get; }

        private double lineHeight;
        private Dictionary<char, double> charWidthMap = new Dictionary<char, double>();

        /// <summary>
        /// Gets the height of the font, adjusting for DPI.
        /// </summary>
        public double LineHeight
        {
            get
            {
                if (lineHeight == 0)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    lineHeight = new FormattedText(" ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, FontSize, Brushes.Black).Height;
#pragma warning restore CS0618 // Type or member is obsolete
                }

                return lineHeight;
            }
        }

        public Font(string name, double fontSize)
        {
            FontFamily = new FontFamily(name);
            Typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            FontSize = fontSize;
        }

        public double GetCharWidth(char c)
        {
            if (charWidthMap.TryGetValue(c, out var value))
                return value;

#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, FontSize, Brushes.Black);
#pragma warning restore CS0618 // Type or member is obsolete
            value = formattedText.WidthIncludingTrailingWhitespace;
            charWidthMap[c] = value;

            return value;
        }
    }
}
