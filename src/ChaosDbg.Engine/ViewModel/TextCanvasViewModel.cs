using ChaosDbg.Text;
using ChaosDbg.Theme;

namespace ChaosDbg.ViewModel
{
    public partial class TextCanvasViewModel : ViewModelBase
    {
        public IUiTextBuffer UiBuffer { get; }

        public Font Font { get; }

        public TextCanvasViewModel(
            IThemeProvider themeProvider,
            ITextBufferProvider textBufferProvider)
        {
            var theme = themeProvider.GetTheme();
            Font = theme.ContentFont;
            var buffer = textBufferProvider.GetBuffer();

            UiBuffer = new UiTextBuffer(buffer, theme.ContentFont);
        }
    }
}
