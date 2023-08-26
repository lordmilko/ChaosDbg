using System;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.Theme;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextBuffer"/> to be displayed in the UI.
    /// </summary>
    public interface IUiTextBuffer : IRenderer
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

        public void Render(DrawingContext drawingContext)
        {
            for (var i = 0; i < Buffer.LineCount; i++)
            {
                var line = Buffer.GetLine(i);

                var uiLine = new UiTextLine(line, font);

                //The IUiTextLine doesn't know what its Y coordinate should be. We apply a transform
                //to the Y axis such that each line will be displayed further and further down the page
                drawingContext.PushTransform(new TranslateTransform(0, i * font.LineHeight));
                uiLine.Render(drawingContext);
                drawingContext.Pop();
            }
        }
    }
}
