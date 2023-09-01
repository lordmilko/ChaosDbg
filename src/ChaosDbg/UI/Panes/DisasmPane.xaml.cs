using ChaosDbg.Theme;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for DisasmPane.xaml
    /// </summary>
    public partial class DisasmPane : ChildUserControl<DisasmPaneViewModel>
    {
        public DisasmPane()
        {
            InitializeComponent();

            var lineHeight = ServiceProvider.GetService<IThemeProvider>().GetTheme().ContentFont.LineHeight;
            ViewModel.AddressChanged += (s, e) => Pane.Canvas.SetVerticalOffset(e.Address * lineHeight);
        }
    }
}
