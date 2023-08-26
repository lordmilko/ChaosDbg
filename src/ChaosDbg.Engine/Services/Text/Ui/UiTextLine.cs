using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Theme;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextLine"/> to be displayed in the UI.
    /// </summary>
    interface IUiTextLine
    {
        IEnumerable<IUiTextRun> Runs { get; }

        void Render(DrawingContext drawingContext);
    }

    /// <summary>
    /// Stores the visual representation of an <see cref="ITextLine"/> to be displayed in the UI.
    /// </summary>
    class UiTextLine : IUiTextLine
    {
        public ITextLine Line { get; }

        private Font font;

        public UiTextLine(ITextLine line, Font font)
        {
            Line = line;
            this.font = font;
        }

        public IEnumerable<IUiTextRun> Runs
        {
            get
            {
                var position = new Point();

                foreach (var run in Line.Runs)
                {
                    //Each line is set to Y=0, and then we use a Transform when rendering to
                    //offset each line correctly
                    var uiRun = new UiTextRun(run, font, position);

                    yield return uiRun;

                    position.X += uiRun.Width;
                }
            }
        }

        public void Render(DrawingContext drawingContext)
        {
            foreach (var run in Runs)
                run.Render(drawingContext);
        }
    }
}
