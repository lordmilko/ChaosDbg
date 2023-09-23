using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    class UiTextLineOrCollection : IRenderable
    {
        protected ITextLineOrCollection Owner { get; }

        public virtual double Width { get; private set; }

        public double Height { get; private set; }

        public double[] CharPositions { get; private set; }

        public IUiTextRun[] Runs
        {
            get
            {
                if (runs == null)
                    throw new InvalidOperationException($"Attempted to access {nameof(Runs)} before {this} had been rendered for the first time.");

                return runs;
            }
        }

        private IUiTextRun[] runs;

        public UiTextLineOrCollection(ITextLineOrCollection owner)
        {
            Owner = owner;
        }

        public void Render(DrawingContext drawingContext, RenderContext renderContext)
        {
            var items = CalculateRuns(renderContext, default);

            foreach (var run in items)
                run.Render(drawingContext, renderContext);

            if (Owner is ITextRun r && Owner.Decorations != null)
            {
                foreach (var decoration in Owner.Decorations)
                    decoration.Render((IUiTextRun) this, drawingContext, renderContext);
            }
        }

        private IUiTextRun[] CalculateRuns(RenderContext renderContext, Point startingPosition)
        {
            var originalX = startingPosition.X;

            if (runs != null)
                return runs;

            var font = renderContext.ThemeProvider.GetTheme().ContentFont;

            Height = font.LineHeight;

            var list = new List<IUiTextRun>();

            foreach (var run in Owner.Runs)
            {
                IUiTextRun uiRun;

                startingPosition.X += run.Style?.Margin.Left ?? 0;

                if (run is ITextRunCollection r)
                {
                    uiRun = new UiTextRunCollection(r, startingPosition);
                }
                else
                {
                    //Each line is set to Y=0, and then we use a Transform when rendering to
                    //offset each line correctly
                    uiRun = new UiTextRun(run, font, startingPosition);
                }

                //TextRunCollection computes its Text, so we should store it in a variable
                var text = run.Text;

                double xPos = 0;

                var charPositions = new double[text.Length + 1];

                for (var i = 0; i < text.Length; i++)
                {
                    charPositions[i] = xPos;

                    var charWidth = font.GetCharWidth(text[i]);
                    xPos += charWidth;
                }

                //We store the X coordinate after the final character so that GetTextPositionFromPoint() can ascertain
                //whether we're before the last character or have gone beyond it
                charPositions[text.Length] = xPos;

                CharPositions = charPositions;

                list.Add(uiRun);

                //Warmup the Width of the inner collection if required
                if (uiRun is UiTextLineOrCollection c)
                    c.CalculateRuns(renderContext, startingPosition);

                startingPosition.X += uiRun.Width;
            }

            Width = startingPosition.X - originalX;

            runs = list.ToArray();

            return runs;
        }
    }
}
