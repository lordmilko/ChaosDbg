using System.Diagnostics;

namespace ChaosDbg.Text
{
    [DebuggerDisplay("Row = {Row}, Column = {Column}")]
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

        public override bool Equals(object obj)
        {
            if (obj is TextPosition p)
                return Row.Equals(p.Row) && Column.Equals(p.Column);

            return false;
        }

        public override int GetHashCode()
        {
            return Row.GetHashCode() ^ Column.GetHashCode();
        }

        public override string ToString()
        {
            return $"({Row},{Column})";
        }
    }
}
