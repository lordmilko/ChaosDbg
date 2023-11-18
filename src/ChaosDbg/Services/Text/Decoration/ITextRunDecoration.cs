using System.Windows.Media;
using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a decoration that can be applied to a specific <see cref="IUiTextRun"/>.
    /// </summary>
    public interface ITextRunDecoration
    {
        void Render(IUiTextRun owner, DrawingContext drawingContext, RenderContext renderContext);
    }
}
