using System.Collections.Generic;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextRun"/> chunks that are displayed on a single line.
    /// </summary>
    public interface ITextLine
    {
        /// <summary>
        /// Gets the <see cref="ITextRun"/> items that this line is comprised of.
        /// </summary>
        IEnumerable<ITextRun> Runs { get; }
    }
}
