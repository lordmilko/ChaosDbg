using System.Windows.Media;
using ChaosDbg.Scroll;

namespace ChaosDbg.Render
{
    /// <summary>
    /// Represents a type of content that is capable of self rendering itself onto a <see cref="DrawingContext"/>.
    /// </summary>
    public interface IRenderable
    {
        void Render(DrawingContext drawingContext, RenderContext renderContext);
    }
}
