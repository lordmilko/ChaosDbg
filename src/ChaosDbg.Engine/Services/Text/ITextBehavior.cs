using System.Windows.Input;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Describes behaviors that can occur when an <see cref="ITextRun"/> is interacted with.
    /// </summary>
    public interface ITextBehavior
    {
        /// <summary>
        /// Gets the cursor that should be used when the mouse is over the <see cref="ITextRun"/>.
        /// </summary>
        Cursor MouseCursor { get; }

        /// <summary>
        /// Gets the text that should be displayed when the mouse hovers over the <see cref="ITextRun"/>.
        /// </summary>
        string HoverText { get; }

        /// <summary>
        /// The action to be performed when the <see cref="ITextRun"/> is clicked.
        /// </summary>
        /// <returns>True if an action was performed, otherwise false.</returns>
        bool OnClick();

        /// <summary>
        /// The action to be performed when the <see cref="ITextRun"/> is right clicked.
        /// </summary>
        /// <returns>True if an action was performed, otherwise false.</returns>
        bool OnRightClick();

        /// <summary>
        /// The action to be performed when the mouse becomes on top of the <see cref="ITextRun"/>
        /// </summary>
        void OnMouseEnter();

        /// <summary>
        /// The action to be performed when the mouse is no longer on top of the <see cref="ITextRun"/>
        /// </summary>
        void OnMouseLeave();
    }
}
