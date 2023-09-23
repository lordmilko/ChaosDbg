using System;
using ChaosDbg.Render;
using ChaosDbg.Theme;
using static ChaosDbg.EventExtensions;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextLine"/> items displayed in a single buffer.
    /// </summary>
    class TextBuffer : ITextBuffer
    {
        public event EventHandler<EventArgs> UpdateBuffer;

        public void RaiseUpdateBuffer(EventArgs args) => HandleSimpleEvent(UpdateBuffer, args);

        public Font Font { get; }

        public int LineCount => lines.Length;

        private ITextLine[] lines;
        private ITextLine[] subsetLines;

        public TextBuffer(Font font, params ITextLine[] lines)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            Font = font;
            this.lines = lines;
            subsetLines = new ITextLine[lines.Length];
        }

        public void PrepareLines(int startIndex, int endIndex)
        {
            Array.Clear(subsetLines, 0, lines.Length);
            //0-0 = 0, but we still want that line itself, so we do +1
            var length = Math.Min(lines.Length, (endIndex - startIndex) + 1);
            Array.Copy(lines, startIndex, subsetLines, 0, length);
        }

        public ITextLine GetLine(int lineIndex, LineMode lineMode) => subsetLines[lineIndex];

        public IRenderable ToRenderable() => new UiTextBuffer(this);
    }
}
