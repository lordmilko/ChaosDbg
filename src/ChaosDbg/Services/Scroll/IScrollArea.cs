using System.Windows.Controls.Primitives;

namespace ChaosDbg.Scroll
{
    /// <summary>
    /// Represents an entity that can be scrolled through.
    /// </summary>
    public interface IScrollArea
    {
        /// <summary>
        /// Gets the height of a single line within the scroll area.<para/>
        /// It is assumed that each line has the same height.
        /// </summary>
        double ScrollLineHeight { get; }

        /// <summary>
        /// Gets the total height of the entity including all virtual sub-components.<para/>
        /// This value can be used to set <see cref="IScrollInfo.ExtentHeight"/>
        /// </summary>
        double ScrollAreaHeight { get; }

        /// <summary>
        /// Gets the total width of the entity including all virtual sub-components.<para/>
        /// This value can be used to set <see cref="IScrollInfo.ExtentWidth"/>
        /// </summary>
        double ScrollAreaWidth { get; }
    }
}
