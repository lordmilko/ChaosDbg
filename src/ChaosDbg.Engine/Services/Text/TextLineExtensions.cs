using System.Linq;
using System.Text;

namespace ChaosDbg.Text
{
    static class TextLineExtensions
    {
        public static int GetLength(this ITextLine line) => line.Runs.Sum(r => r.Text.Length);

        public static string GetText(this ITextLine line)
        {
            var builder = new StringBuilder();

            foreach (var run in line.Runs)
                builder.Append(run);

            return builder.ToString();
        }
    }
}
