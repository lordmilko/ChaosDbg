using System.Windows.Media;

namespace ChaosDbg.Theme
{
    public interface ITheme
    {
        /// <summary>
        /// Gets the font used for displaying content within UI panes.
        /// </summary>
        Font ContentFont { get; }

        Brush TableBorderBrush { get; }

        Color BackgroundColor { get; }
    }

    class Theme : ITheme
    {
        public Font ContentFont { get; }

        public Brush TableBorderBrush { get; }

        public Color BackgroundColor { get; }

        public Theme()
        {
            ContentFont = new Font("Consolas", 13.5);
            TableBorderBrush = Brushes.Gray;
            BackgroundColor = Color.FromRgb(0xFF, 0xF8, 0xF0);
        }
    }
}
