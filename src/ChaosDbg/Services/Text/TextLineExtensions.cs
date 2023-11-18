using System.Linq;
using System.Text;

namespace ChaosDbg.Text
{
    static class TextLineExtensions
    {
        public static int GetLength(this ITextLineOrCollection lineOrCollection) => lineOrCollection.Runs.Sum(r => r.Text.Length);

        public static string GetText(this ITextLineOrCollection lineOrCollection)
        {
            var builder = new StringBuilder();

            foreach (var run in lineOrCollection.Runs)
                builder.Append(run);

            return builder.ToString();
        }
    }
}
