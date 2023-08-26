using System;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextLine"/> items displayed in a single buffer.
    /// </summary>
    class TextBuffer : ITextBuffer
    {
        public int LineCount => lines.Length;

        private ITextLine[] lines;

        public TextBuffer(params ITextLine[] lines)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            this.lines = lines;
        }

        public ITextLine GetLine(int index) => lines[index];
    }
}
