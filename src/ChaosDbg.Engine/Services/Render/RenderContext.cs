using System.Windows;
using System.Windows.Media;
using ChaosDbg.Scroll;
using ChaosDbg.Theme;

namespace ChaosDbg.Render
{
    public class RenderContext
    {
        public ScrollManager ScrollManager { get; }

        public IThemeProvider ThemeProvider { get; }

        public DpiScale Dpi { get; }

        public RenderContext(ScrollManager scrollManager, IThemeProvider themeProvider)
        {
            ScrollManager = scrollManager;
            ThemeProvider = themeProvider;
            Dpi = VisualTreeHelper.GetDpi((Visual)scrollManager.Scrollee);
        }
    }
}
