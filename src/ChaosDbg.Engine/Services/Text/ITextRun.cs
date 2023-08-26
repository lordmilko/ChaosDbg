namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a chunk of text in an <see cref="ITextLine"/>.
    /// </summary>
    public interface ITextRun
    {
        /// <summary>
        /// Gets the text that this run encapsulates.
        /// </summary>
        string Text { get; }
    }
}
