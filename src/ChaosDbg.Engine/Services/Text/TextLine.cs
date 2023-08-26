using System;
using System.Collections.Generic;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextRun"/> chunks that are displayed on a single line.
    /// </summary>
    class TextLine : ITextLine
    {
        public IEnumerable<ITextRun> Runs { get; }

        public TextLine(params ITextRun[] runs)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            Runs = runs;
        }
    }
}
