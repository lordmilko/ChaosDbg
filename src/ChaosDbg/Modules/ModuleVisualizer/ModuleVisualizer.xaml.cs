using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for ModuleVisualizer.xaml
    /// </summary>
    public partial class ModuleVisualizer : ChildUserControl<ModuleVisualizerViewModel>
    {
        public ModuleVisualizer()
        {
            InitializeComponent();

            ViewModel.RenderContent.Changed += (s, e) => Canvas.InvalidateVisual();
        }
    }
}
