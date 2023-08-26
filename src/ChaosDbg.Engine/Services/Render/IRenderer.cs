using System.Windows.Media;
using ChaosDbg.Scroll;

namespace ChaosDbg.Render
{
    public interface IRenderer
    {
        void Render(DrawingContext drawingContext, ScrollManager scrollManager);
    }
}
