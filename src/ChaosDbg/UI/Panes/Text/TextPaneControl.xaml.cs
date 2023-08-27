using System.Windows;
using ChaosDbg.Render;
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
            new PropertyMetadata()
        );

        public IRenderable RenderContent
        {
            get => (IRenderable) GetValue(RenderContentProperty);
            set => SetValue(RenderContentProperty, value);
        }

        #endregion

        public TextPaneControl()
        {
            InitializeComponent();
        }
    }
}
