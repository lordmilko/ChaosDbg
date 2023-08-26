namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextLine"/> items displayed in a single buffer.
    /// </summary>
    public interface ITextBuffer
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
}
