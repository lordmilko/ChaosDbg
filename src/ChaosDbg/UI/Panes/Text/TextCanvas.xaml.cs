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
        #region Content

        public static readonly DependencyProperty RenderContentProperty = DependencyProperty.Register(
            nameof(RenderContent),
            typeof(IRenderable),
            typeof(TextCanvas),
            new PropertyMetadata(null, RenderContentChanged)
        );

        public IRenderable RenderContent
        {
            get => (IRenderable) GetValue(RenderContentProperty);
            set => SetValue(RenderContentProperty, value);
        }

        private static void RenderContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is IRenderable r)
            {
                var canvas = (TextCanvas)d;

                canvas.RenderManager = new TwoStageRenderManager(r, canvas.OnRender);

                canvas.scrollManager = new Lazy<ScrollManager>(() =>
                {
                    var manager = new ScrollManager(canvas, r);

                    //Neither the logical nor visual tree has been constructed yet. Waiting for the loaded event is too long,
                    //we need to be accessible as soon as possible, so we can be used during arrange, etc. Thus, we lazily initialize
                    //ourselves. By the time we're accessed, we can probably be used
                    var parentPane = canvas.GetAncestor<TextPaneControl>();
                    var scrollViewer = parentPane.GetLogicalDescendant<ScrollViewer>();

                    scrollViewer.ScrollChanged += manager.ScrollViewerScrollChanged;
                    scrollViewer.PreviewMouseWheel += manager.ScrollViewerMouseWheel;

                    return manager;
                });

                //Force a re-render now our properties have been set
                canvas.InvalidateVisual();
            }
        }

        #endregion

        public TwoStageRenderManager RenderManager { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ScrollManager ScrollManager => scrollManager.Value;

        private Lazy<ScrollManager> scrollManager;

        public TextCanvas()
        {
            InitializeComponent();
        }

        protected override void OnRender(DrawingContext dc)
        {
            //A single render pass occurs before property binding occurs. As such, if RenderContent
            //is not set yet, we haven't set our RenderManager yet either. We'll force a refresh via
            //InvalidateVisual once the RenderContent is set
            if (RenderManager != null && !RenderManager.IsBaseRendering)
                RenderManager.Render(dc, ScrollManager);
            else
                base.OnRender(dc);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            //If the RenderContent has not been set yet, we won't have a ScrollManager to interact with yet.
            //The first render pass occurs before property binding has occurred. RenderContent will call
            //InvalidateVisual upon being bound, forcing a refresh here
            if (scrollManager != null)
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

        public double ExtentWidth => scrollManager?.Value.ExtentWidth ?? 0;
        public double ExtentHeight => ScrollManager.ExtentHeight;
        public double ViewportWidth => scrollManager?.Value.ViewportWidth ?? 0;
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
