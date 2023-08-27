using System;
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

        private Font font;

        public UiTextBuffer(ITextBuffer buffer, Font font)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (font == null)
                throw new ArgumentNullException(nameof(font));

            Buffer = buffer;
            this.font = font;
        }

        #region IUiTextBuffer

        public void Render(DrawingContext drawingContext, ScrollManager scrollManager)
        {
            var range = scrollManager.GetVisibleTextRange();

            var offset = range.Start.Row;

            var maxWidth = 0;

            for (var i = range.Start.Row; i < range.End.Row; i++)
            {
                var line = Buffer.GetLine(i);

                maxWidth = Math.Max(maxWidth, line.GetLength());

                var uiLine = new UiTextLine(line, font);

                //The IUiTextLine doesn't know what its Y coordinate should be. We apply a transform
                //to the Y axis such that each line will be displayed further and further down the page
                drawingContext.PushTransform(new TranslateTransform(0, (i - offset) * font.LineHeight));
                uiLine.Render(drawingContext);
                drawingContext.Pop();
            }

            ScrollAreaWidth = maxWidth;
        }

        #endregion
        #region IScrollArea

        public double ScrollLineHeight => font.LineHeight;

        public double ScrollAreaWidth { get; private set; }

        public double ScrollAreaHeight => Buffer.LineCount * font.LineHeight;

        #endregion
    }
}
