using System;
using System.Diagnostics;
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
    public partial class TextCanvas : CanvasBase<TextCanvasViewModel>, IRenderManagerProvider, IScrollInfo
    {
        public TwoStageRenderManager RenderManager { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ScrollManager ScrollManager => scrollManager.Value;

        private Lazy<ScrollManager> scrollManager;

        public TextCanvas()
        {
            InitializeComponent();

            RenderManager = new TwoStageRenderManager(ViewModel.UiBuffer, base.OnRender);

            scrollManager = new Lazy<ScrollManager>(() =>
            {
                var manager = new ScrollManager(this, ViewModel.UiBuffer);

                //Neither the logical nor visual tree has been constructed yet. Waiting for the loaded event is too long,
                //we need to be accessible as soon as possible, so we can be used during arrange, etc. Thus, we lazily initialize
                //ourselves. By the time we're accessed, we can probably be used
                var parentPane = this.GetAncestor<TextPaneControl>();
                var scrollViewer = parentPane.GetLogicalDescendant<ScrollViewer>();

                scrollViewer.ScrollChanged += manager.ScrollViewerScrollChanged;
                scrollViewer.PreviewMouseWheel += manager.ScrollViewerMouseWheel;

                return manager;
            });
        }

        protected override void OnRender(DrawingContext dc) =>
            RenderManager.Render(dc, ScrollManager);

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            ScrollManager.ViewportSize = arrangeSize;
            ScrollOwner.InvalidateScrollInfo();

            return base.ArrangeOverride(arrangeSize);
        }

        #region IScrollInfo

        public ScrollViewer ScrollOwner { get; set; }

        //Unused
        public bool CanVerticallyScroll { get; set; }
        public bool CanHorizontallyScroll { get; set; }

        public double ExtentWidth => ScrollManager.ExtentWidth;
        public double ExtentHeight => ScrollManager.ExtentHeight;
        public double ViewportWidth => ScrollManager.ViewportWidth;
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
        public void SetVerticalOffset(double offset) => ScrollManager.SetVerticalOffset(offset);
        public Rect MakeVisible(Visual visual, Rect rectangle) => ScrollManager.MakeVisible(visual, rectangle);

        #endregion
    }
}
