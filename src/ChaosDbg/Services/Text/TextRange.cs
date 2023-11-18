namespace ChaosDbg.Text
{
    public struct TextRange
    {
        /// <summary>
        /// Gets the start position of the text range.
        /// </summary>
        public TextPosition Start { get; }

        /// <summary>
        /// Gets the end position of the text range.
        /// </summary>
        public TextPosition End { get; set; }

        public TextRange(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }
    }
}
