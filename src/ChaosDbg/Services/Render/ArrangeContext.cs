using System.Windows;
using ChaosDbg.Theme;

namespace ChaosDbg.Render
{
    public class ArrangeContext
    {
        public double AvailableWidth { get; }

        public double AvailableHeight { get; }

        public IThemeProvider ThemeProvider { get; }

        public double xPos { get; set; }

        public double yPos { get; set; }

        public double MaxXPos { get; set; }

        public ArrangeContext(Size size, IThemeProvider themeProvider)
        {
            AvailableWidth = size.Width;
            AvailableHeight = size.Height;
            ThemeProvider = themeProvider;
        }
    }
}
