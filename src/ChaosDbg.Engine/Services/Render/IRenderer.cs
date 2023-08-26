using System.Windows.Media;

namespace ChaosDbg.Render
{
    public interface IRenderer
    {
        void Render(DrawingContext drawingContext);
    }
}
