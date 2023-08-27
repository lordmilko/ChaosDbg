using System.Windows;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a set of custom styles that can be applied to a text node.
    /// </summary>
    public interface ITextStyle
    {
        /// <summary>
        /// Gets or sets the spacing around an element. Currently only <see cref="Thickness.Left"/> and <see cref="Thickness.Right"/> are considered.
        /// </summary>
        Thickness Margin { get; set; }
    }

    /// <inheritdoc cref="ITextStyle" />
    public class TextStyle : ITextStyle
    {
        public Thickness Margin { get; set; }
    }
}
