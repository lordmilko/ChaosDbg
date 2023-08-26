namespace ChaosDbg.Text
{
    public struct TextPosition
    {
        /// <summary>
        /// Gets the absolute row number this position represents.
        /// </summary>
        public int Row { get; }

        /// <summary>
        /// Gets the absolute row number this position represents.
        /// </summary>
        public int Column { get; }

        public TextPosition(int row, int column)
        {
            Row = row;
            Column = column;
        }
    }
}
