using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.DbgEng;
using ChaosDbg.Render;

namespace ChaosDbg.ViewModel
{
    public class ModuleVisualizerContent : IRenderable
    {
        public event EventHandler<EventArgs> Changed;

        private List<IDbgModule> modules = new List<IDbgModule>();

        public void AddModule(IDbgModule module)
        {
            modules.Add(module);
            EventExtensions.HandleEvent(Changed, EventArgs.Empty);
        }

        public void RemoveModule(IDbgModule module)
        {
            modules.Remove(module);
            EventExtensions.HandleEvent(Changed, EventArgs.Empty);
        }

        public void Render(DrawingContext drawingContext, RenderContext renderContext)
        {
            //In order to get perfect 1 pixel wide lines we must do 2 things:
            //1. Call RenderOptions.SetEdgeMode(this, EdgeMode.Aliased) in the owning control. This gets rid of the grey aliasing line
            //2. Scale the size of the brush by 1/PixelsPerDip. e.g. at 125% scaling the width is 0.8

            var thickness = 1 / renderContext.Dpi.PixelsPerDip;

            Random r = new Random(300);

            var width = renderContext.Owner.ActualWidth;

            var moduleWidths = modules.Sum(m => m.Size);
            var maxLines = width / thickness;

            var relativeModules = modules.Select(m => new
            {
                M = m,
                Percentage = ((double)m.Size / moduleWidths),
                Pixels = width * ((double)m.Size / moduleWidths),
                Lines = Math.Round(maxLines * ((double)m.Size / moduleWidths))
            }).ToArray();

            for (var i = 0; i < relativeModules.Length; i++)
            {
                var offset = relativeModules.Take(i).Sum(v => v.Lines);

                drawingContext.PushTransform(new TranslateTransform(offset * thickness, 0));

                Brush brush = new SolidColorBrush(Color.FromRgb((byte)r.Next(1, 255),
                    (byte)r.Next(1, 255), (byte)r.Next(1, 233)));

                var pen = new Pen(brush, thickness);

#pragma warning disable CS0618 // Type or member is obsolete
                var text = new FormattedText(relativeModules[i].M.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 14, Brushes.Black);
#pragma warning restore CS0618 // Type or member is obsolete

                for (var j = 0; j < relativeModules[i].Lines; j++)
                    drawingContext.DrawLine(pen, new Point(j * thickness, 0), new Point(j * thickness, renderContext.Owner.ActualHeight));

                drawingContext.DrawText(text, new Point(0, 0));

                drawingContext.Pop();
            }
        }
    }
}
