using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.Scroll;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for TextCanvasControl.xaml
    /// </summary>
    public partial class TextCanvas : CanvasBase<TextCanvasViewModel>, IDrawable, IScrollInfo
    {
        #region Content

        public static readonly DependencyProperty RenderContentProperty = DependencyProperty.Register(
            nameof(RenderContent),
            typeof(IRenderable),
            typeof(TextCanvas),
            new PropertyMetadata()
        );

        public IRenderable RenderContent
        {
            get => (IRenderable) GetValue(RenderContentProperty);
            set => SetValue(RenderContentProperty, value);
        }

        #endregion

        public DrawingGroup DrawingGroup { get; } = new DrawingGroup();
        public ScrollManager ScrollManager { get; set; }
        public RenderContext RenderContext { get; set; }

        public TextCanvas()
        {
            InitializeComponent();

            //In order to display pixel perfect table lines (without any blurring at higher DPIs) we must
            //set the EdgeMode to Aliased
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (RenderContent != null)
            {
                using (var internalCtx = DrawingGroup.Open())
                    RenderContent.Render(internalCtx, RenderContext);

                dc.DrawDrawing(DrawingGroup);
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            //If the RenderContent has not been set yet, we won't have a ScrollManager to interact with yet.
            //The first render pass occurs before property binding has occurred. RenderContent will call
            //InvalidateVisual upon being bound, forcing a refresh here
            if (ScrollManager != null)
            {
                ScrollManager.ViewportSize = arrangeSize;
                ScrollOwner.InvalidateScrollInfo();
            }

            return base.ArrangeOverride(arrangeSize);
        }

        #region IScrollInfo

        public ScrollViewer ScrollOwner { get; set; }

        //Unused
        public bool CanVerticallyScroll { get; set; }
        public bool CanHorizontallyScroll { get; set; }

        public double ExtentWidth => ScrollManager?.ExtentWidth ?? 0;
        public double ExtentHeight => ScrollManager.ExtentHeight;
        public double ViewportWidth => ScrollManager?.ViewportWidth ?? 0;
        public double ViewportHeight => ScrollManager.ViewportHeight;
        public double HorizontalOffset => ScrollManager.HorizontalOffset;
        public double VerticalOffset => ScrollManager.VerticalOffset;
        public void LineUp() => ScrollManager.LineUp();
        public void LineDown() => ScrollManager.LineDown();
        public void LineLeft() => ScrollManager.LineLeft();
        public void LineRight() => ScrollManager.LineRight();
        public void PageUp() => ScrollManager.PageUp();
        public void PageDown() => ScrollManager.PageDown();
        public void PageLeft() => ScrollManager.PageLeft();
        public void PageRight() => ScrollManager.PageRight();
        public void MouseWheelUp() => ScrollManager.MouseWheelUp();
        public void MouseWheelDown() => ScrollManager.MouseWheelDown();
        public void MouseWheelLeft() => ScrollManager.MouseWheelLeft();
        public void MouseWheelRight() => ScrollManager.MouseWheelRight();
        public void SetHorizontalOffset(double offset) => ScrollManager.SetHorizontalOffset(offset);

        /// <summary>
        /// Sets the current vertical offset in pixels between 0 and <see cref="ExtentHeight"/>.
        /// </summary>
        /// <param name="offset">The pixel offset to set the vertical scrollbar to.</param>
        public void SetVerticalOffset(double offset) => ScrollManager.SetVerticalOffset(offset);

        public Rect MakeVisible(Visual visual, Rect rectangle) => ScrollManager.MakeVisible(visual, rectangle);

        #endregion
    }
}
