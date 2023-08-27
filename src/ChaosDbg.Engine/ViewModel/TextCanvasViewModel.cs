using System.Windows.Media;
using ChaosDbg.Reactive;
using ChaosDbg.Theme;

namespace ChaosDbg.ViewModel
{
    public partial class TextCanvasViewModel : ViewModelBase
    {
        [Reactive]
        public virtual Brush BackgroundColor { get; set; }

        public TextCanvasViewModel(IThemeProvider themeProvider)
        {
            BackgroundColor = new SolidColorBrush(themeProvider.GetTheme().BackgroundColor);
        }
    }
}
