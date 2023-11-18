using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a decoration that adds a vertical line after an <see cref="ITextRun"/>.
    /// </summary>
    class RightBorderRunDecoration : ITextRunDecoration
    {
        public void Render(IUiTextRun owner, DrawingContext drawingContext, RenderContext renderContext)
        {
            var theme = renderContext.ThemeProvider.GetTheme();

            //In order to get perfect 1 pixel wide lines we must do 2 things:
            //1. Call RenderOptions.SetEdgeMode(this, EdgeMode.Aliased) in the owning control. This gets rid of the grey aliasing line
            //2. Scale the size of the brush by 1/PixelsPerDip. e.g. at 125% scaling the width is 0.8

            var thickness = 1 / renderContext.Dpi.PixelsPerDip;

            var pen = new Pen(theme.TableBorderBrush, thickness);

            //When our line is drawn, a transform is applied that modifies where our Y axis starts
            drawingContext.DrawLine(
                pen,
                new Point(owner.Position.X + owner.Width, 0),
                new Point(owner.Position.X + owner.Width, owner.Height)
            );
        }
    }
}
