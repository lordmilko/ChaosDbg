using System.Text;

namespace ChaosDbg.Text
{
    static class TextBufferExtensions
    {
        public static string GetTextForRange(this ITextBuffer buffer, TextRange range)
        {
            var builder = new StringBuilder();

            for (var i = range.Start.Row; i <= range.End.Row; i++)
            {
                var line = buffer.GetLine(i, LineMode.Absolute).GetText();

                var startCol = 0;
                var endCol = 0;

                //If we're selecting across multiple rows, when we wrap around to the second row
                //the column will always start from 0. Thus we only set the start column on the first row
                if (i == range.Start.Row)
                    startCol = range.Start.Column;

                //If this is the last row, we end at the column that was specified. Otherwise, we're wrapping
                //from this row to another row, so we need to select until the end of the row
                if (i == range.End.Row)
                    endCol = range.End.Column;
                else
                    endCol = line.Length;

                var str = line.Substring(startCol, endCol - startCol);

                builder.Append(str);

                if (i < range.End.Row)
                    builder.AppendLine();
            }

            return builder.ToString();
        }

        public static ITextRun GetRunAtTextPosition(this ITextBuffer buffer, TextPosition position)
        {
            if (position.Row > buffer.LineCount)
                return null;

            var line = buffer.GetLine(position.Row, LineMode.Absolute);

            var column = position.Column;

            foreach (var run in line.Runs)
            {
                if (column < run.Text.Length)
                    return run;

                column -= run.Text.Length;
            }

            return null;
        }
    }
}
