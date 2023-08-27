using System.Windows;
using System.Windows.Media;

namespace ChaosDbg.Tests
{
    class LineDrawingInfo : DrawingInfo
    {
        public Pen Pen { get; }

        public Point StartPoint { get; }

        public Point EndPoint { get; }

        public LineDrawingInfo(GeometryDrawing drawing)
        {
            var line = (LineGeometry) drawing.Geometry;

            Pen = drawing.Pen;
            StartPoint = line.StartPoint;
            EndPoint = line.EndPoint;
        }
    }
}
