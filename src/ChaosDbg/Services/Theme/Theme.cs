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

        Color PaneBackgroundColor { get; }

        Color WindowBackgroundColor { get; }

        Color DockPreviewBackgroundColor { get; }
    }

    class Theme : ITheme
    {
        public Font ContentFont { get; }

        public Brush TableBorderBrush { get; }

        public Color PaneBackgroundColor { get; }

        public Color WindowBackgroundColor { get; }

        public Color DockPreviewBackgroundColor { get; }

        public Theme()
        {
            ContentFont = new Font("Consolas", 13.5);
            TableBorderBrush = Brushes.Gray;
            PaneBackgroundColor = Color.FromRgb(0xFF, 0xF8, 0xF0);
            WindowBackgroundColor = Color.FromRgb(0xEE, 0xEE, 0xF2);

            //The colour is dark, but after opacity is applied it'll look light
            DockPreviewBackgroundColor = Color.FromArgb(0xFF, 0x32, 0x99, 0xFF); //Not 100% if this is it
        }
    }
}
