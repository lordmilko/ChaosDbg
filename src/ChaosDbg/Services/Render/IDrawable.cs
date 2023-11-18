using System.Windows.Media;

namespace ChaosDbg.Render
{
    interface IDrawable
    {
        DrawingGroup DrawingGroup { get; }
    }
}
