using System.Linq;

namespace ChaosDbg.Text
{
    static class TextLineExtensions
    {
        public static int GetLength(this ITextLine line) => line.Runs.Sum(r => r.Text.Length);
    }
}
