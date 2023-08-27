using System;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.Scroll;
using ChaosDbg.Theme;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextBuffer"/> to be displayed in the UI.
    /// </summary>
    public interface IUiTextBuffer : IRenderable, IScrollArea
    {
        ITextBuffer Buffer { get; }
    }

    /// <summary>
    /// Stores the visual representation of an <see cref="ITextBuffer"/> to be displayed in the UI.
    /// </summary>
    class UiTextBuffer : IUiTextBuffer
    {
        public ITextBuffer Buffer { get; }

        public UiTextBuffer(ITextBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Buffer = buffer;
        }

        #region IUiTextBuffer

        public void Render(DrawingContext drawingContext, RenderContext renderContext)
        {
            var scrollManager = renderContext.ScrollManager;
            var theme = renderContext.ThemeProvider.GetTheme();

            var font = theme.ContentFont;

            ScrollLineHeight = font.LineHeight;
            ScrollAreaHeight = Buffer.LineCount * font.LineHeight;

            var range = scrollManager.GetVisibleTextRange();

            var offset = range.Start.Row;

            var maxWidth = 0;

            var scrollPos = scrollManager.ScrollPositionPixels;

            var startPixel = ScrollLineHeight * range.Start.Row;
            var scrollOffset = new Point(scrollPos.X, scrollPos.Y - startPixel);

            var clipGeometry = new RectangleGeometry(new Rect(scrollManager.ViewportSize));
            drawingContext.PushClip(clipGeometry);
            drawingContext.PushTransform(new ScaleTransform(scrollManager.Zoom, scrollManager.Zoom));

            //This line is very important. Without it, when we scroll to the bottom of a list, part of the last line is cutoff and doesn't display properly
            drawingContext.PushTransform(new TranslateTransform(-scrollOffset.X, -scrollOffset.Y));

            for (var i = range.Start.Row; i < range.End.Row; i++)
            {
                var line = Buffer.GetLine(i);

                maxWidth = Math.Max(maxWidth, line.GetLength());

                var uiLine = new UiTextLine(line);

                //The IUiTextLine doesn't know what its Y coordinate should be. We apply a transform
                //to the Y axis such that each line will be displayed further and further down the page

                var offsetY = (i - offset) * font.LineHeight;

                drawingContext.PushTransform(new TranslateTransform(0, offsetY));
                uiLine.Render(drawingContext, renderContext);
                drawingContext.Pop();
            }

            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();

            ScrollAreaWidth = maxWidth;
        }

        #endregion
        #region IScrollArea

        public double ScrollLineHeight { get; private set; }

        public double ScrollAreaWidth { get; private set; }

        public double ScrollAreaHeight { get; private set; }

        #endregion
    }
}
