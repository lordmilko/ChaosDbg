using System;
using ChaosDbg.Render;
using ChaosDbg.Theme;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextLine"/> items displayed in a single buffer.
    /// </summary>
    public interface ITextBuffer : IConvertableToRenderable
    {
        /// <summary>
        /// The event that occurs when the buffer needs to be updated.
        /// </summary>
        event EventHandler<EventArgs> UpdateBuffer;

        /// <summary>
        /// Raises the <see cref="UpdateBuffer"/> event.
        /// </summary>
        /// <param name="args">The arguments that provide information about the text that has changed</param>
        void RaiseUpdateBuffer(EventArgs args);

        Font Font { get; }

        /// <summary>
        /// Gets the number of lines contained in the buffer.
        /// </summary>
        int LineCount { get; }

        /// <summary>
        /// Notifies the buffer of the range of lines that will be requested.
        /// </summary>
        /// <param name="startIndex">The absolute index of the first line to retrieve.</param>
        /// <param name="endIndex">The absolute index of the last line to retrieve.</param>
        void PrepareLines(int startIndex, int endIndex);

        /// <summary>
        /// Gets the <see cref="ITextLine"/> at the specified index.
        /// </summary>
        /// <param name="lineIndex">The index of the line to retrieve, relative to the subset of lines collected in <see cref="PrepareLines(int, int)"/>.</param>
        /// <returns>The retrieved <see cref="ITextLine"/>.</returns>
        ITextLine GetLine(int lineIndex);
    }
}
