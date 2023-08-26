using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for TextCanvasControl.xaml
    /// </summary>
    public partial class TextCanvas : CanvasBase<TextCanvasViewModel>, IRenderManagerProvider
    {
        public TwoStageRenderManager RenderManager { get; }

        public TextCanvas()
        {
            InitializeComponent();

            RenderManager = new TwoStageRenderManager(ViewModel.UiBuffer, base.OnRender);
        }

        protected override void OnRender(DrawingContext dc) => RenderManager.Render(dc);

        protected override Size MeasureOverride(Size constraint)
        {
            return new Size(0, ViewModel.UiBuffer.Buffer.LineCount * ViewModel.Font.LineHeight);
        }
    }
}
