using System;
using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextLine"/> items displayed in a single buffer.
    /// </summary>
    public interface ITextBuffer : IConvertableToRenderable
    {
        /// <summary>
        /// Gets the number of lines contained in the buffer.
        /// </summary>
        int LineCount { get; }

        /// <summary>
        /// Gets the <see cref="ITextLine"/> at the specified index.
        /// </summary>
        /// <param name="index">The index of the line to retrieve.</param>
        /// <returns>The retrieved <see cref="ITextLine"/>.</returns>
        ITextLine GetLine(int index);
    }

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

        public IRenderable ToRenderable() => new UiTextBuffer(this);
    }
}
