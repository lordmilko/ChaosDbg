using System.Windows;
using System.Windows.Media;
using ChaosDbg.Scroll;
using ChaosDbg.Theme;

namespace ChaosDbg.Render
{
    public class RenderContext
    {
        public FrameworkElement Owner { get; }

        public ScrollManager ScrollManager { get; set; }

        public IThemeProvider ThemeProvider { get; }

        public DpiScale Dpi { get; }

        public RenderContext(FrameworkElement owner, IThemeProvider themeProvider)
        {
            Owner = owner;
            ThemeProvider = themeProvider;
            Dpi = VisualTreeHelper.GetDpi(owner);
        }
    }
}
