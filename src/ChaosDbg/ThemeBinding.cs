using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ChaosDbg.Theme;

namespace ChaosDbg
{
    static class ThemeBinding
    {
        private static Dictionary<string, Brush> cache = new Dictionary<string, Brush>();

        private static ITheme theme;

        private static ITheme Theme
        {
            get
            {
                if (theme == null)
                    theme = GlobalProvider.ServiceProvider.GetService<IThemeProvider>().GetTheme();

                return theme;
            }
        }

        public static Brush WindowBackgroundBrush => GetOrAddBrush(Theme.WindowBackgroundColor);

        public static Brush DockPreviewBackgroundColor => GetOrAddBrush(Theme.DockPreviewBackgroundColor);

        private static Brush GetOrAddBrush(Color color, [CallerMemberName] string callerMemberName = null)
        {
            Debug.Assert(callerMemberName != null);

            if (cache.TryGetValue(callerMemberName, out var existing))
                return existing;

            existing = new SolidColorBrush(color);
            cache[callerMemberName] = existing;
            return existing;
        }
    }
}
