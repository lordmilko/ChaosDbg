using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextLine"/> to be displayed in the UI.
    /// </summary>
    interface IUiTextLine : IRenderable
    {
        double[] CharPositions { get; }

        IUiTextRun[] Runs { get; }
    }

    /// <summary>
    /// Stores the visual representation of an <see cref="ITextLine"/> to be displayed in the UI.
    /// </summary>
    class UiTextLine : UiTextLineOrCollection, IUiTextLine
    {
        public ITextLine Line => (ITextLine) base.Owner;

        public UiTextLine(ITextLine line) : base(line)
        {
        }
    }
}
