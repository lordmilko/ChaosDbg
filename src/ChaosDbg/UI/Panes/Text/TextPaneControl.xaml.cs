using System.Windows;
using ChaosDbg.DbgEng;
using ChaosDbg.Render;
using ChaosDbg.Scroll;
using ChaosDbg.Text;
using ChaosDbg.Theme;
using ChaosDbg.ViewModel;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for TextPaneControl.xaml
    /// </summary>
    public partial class TextPaneControl : ChildUserControl<TextPaneControlViewModel>
    {
        #region RawContent

        public static readonly DependencyProperty RawContentProperty = DependencyProperty.Register(
            nameof(RawContent),
            typeof(IConvertableToRenderable),
            typeof(TextPaneControl),
            new PropertyMetadata(null, RawContentChanged)
        );

        public IConvertableToRenderable RawContent
        {
            get => (IConvertableToRenderable) GetValue(RawContentProperty);
            set => SetValue(RawContentProperty, value);
        }

        private static void RawContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is IConvertableToRenderable c)
                d.SetValue(RenderContentProperty, c.ToRenderable());
        }

        #endregion
        #region Content

        public static readonly DependencyProperty RenderContentProperty = DependencyProperty.Register(
            nameof(RenderContent),
            typeof(IRenderable),
            typeof(TextPaneControl),
            new PropertyMetadata(null, RenderContentChanged)
        );

        public IUiTextBuffer RenderContent
        {
            get => (IUiTextBuffer) GetValue(RenderContentProperty);
            set => SetValue(RenderContentProperty, value);
        }

        private static void RenderContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var @this = (TextPaneControl) d;

            if (@this.Canvas.ScrollManager != null)
            {
                //Unregister the event handlers we previously installed on the old scroll manager
                var oldManager = @this.Canvas.ScrollManager;

                @this.ScrollViewer.ScrollChanged -= oldManager.ScrollViewerScrollChanged;
                @this.ScrollViewer.PreviewMouseWheel -= oldManager.ScrollViewerMouseWheel;
            }

            ScrollManager manager;

            if (e.NewValue is IUiTextBuffer u && u.Buffer is IScrollInterceptor i)
                manager = new LogicalScrollManager(@this.Canvas, (IScrollArea) e.NewValue, i);
            else
                manager = new ScrollManager(@this.Canvas, (IScrollArea) e.NewValue);

            @this.Canvas.ScrollManager = manager;
            @this.Canvas.RenderContext = new RenderContext(manager, GlobalProvider.ServiceProvider.GetService<IThemeProvider>());

            @this.ScrollViewer.ScrollChanged += manager.ScrollViewerScrollChanged;
            @this.ScrollViewer.PreviewMouseWheel += manager.ScrollViewerMouseWheel;

            ((IUiTextBuffer) e.NewValue).UpdateBuffer += (_1, _2) => manager.RequestInvalidateScrolledArea();
        }

        #endregion

        public TextPaneControl()
        {
            InitializeComponent();
        }
    }
}
