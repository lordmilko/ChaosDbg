using System.Collections.Generic;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a node that is either a <see cref="ITextLine"/> or <see cref="ITextRunCollection"/>
    /// </summary>
    public interface ITextLineOrCollection
    {
        /// <summary>
        /// Gets the <see cref="ITextRun"/> items that this line or collection is comprised of.
        /// </summary>
        IEnumerable<ITextRun> Runs { get; }

        /// <summary>
        /// Gets or sets the decorations that are used to draw custom elements against this object.
        /// </summary>
        ITextRunDecoration[] Decorations { get; set; }
    }
}
