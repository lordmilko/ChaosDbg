using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.Theme;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextRun"/> to be displayed in the UI.
    /// </summary>
    public interface IUiTextRun : IRenderable
    {
        Point Position { get; }

        /// <summary>
        /// Gets the width of the run based on its specified font.
        /// </summary>
        double Width { get; }

        /// <summary>
        /// Gets the hight of the run based on its specified font.
        /// </summary>
        double Height { get; }
    }

    class UiTextRun : IUiTextRun
    {
        /// <summary>
        /// Gets the formatted text of this text run.
        /// </summary>
        public FormattedText FormattedText { get; }

        /// <summary>
        /// Gets the position that this run should be drawn at.
        /// </summary>
        public Point Position { get; }

        /// <summary>
        /// Gets the <see cref="ITextRun"/> that this visual run encapsulates.
        /// </summary>
        public ITextRun Run { get; }

        public double Height => FormattedText.Height;

        public double Width
        {
            get
            {
                var width = FormattedText.WidthIncludingTrailingWhitespace;

                if (Run.Style != null)
                    width += Run.Style.Margin.Right;

                return width;
            }
        }

        public UiTextRun(ITextRun run, Font font, Point position)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

#pragma warning disable CS0618 // Type or member is obsolete
            FormattedText = new FormattedText(
                run.Text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                font.Typeface,
                font.FontSize,
                Brushes.Black
            );
#pragma warning restore CS0618 // Type or member is obsolete

            Position = position;
            Run = run;
        }

        public void Render(DrawingContext drawingContext, RenderContext renderContext)
        {
            drawingContext.DrawText(FormattedText, Position);
        }
    }
}
