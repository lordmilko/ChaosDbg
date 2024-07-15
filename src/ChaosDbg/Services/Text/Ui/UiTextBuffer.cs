using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;
using ChaosDbg.Scroll;
using static ChaosDbg.EventExtensions;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Stores the visual representation of an <see cref="ITextBuffer"/> to be displayed in the UI.
    /// </summary>
    public interface IUiTextBuffer : IRenderable, IScrollArea
    {
        /// <summary>
        /// The event that occurs when the buffer needs to be updated.
        /// </summary>
        event EventHandler<EventArgs> UpdateBuffer;

        /// <summary>
        /// Raises the <see cref="UpdateBuffer"/> event.
        /// </summary>
        /// <param name="args">The arguments that provide information about the text that has changed</param>
        void RaiseUpdateBuffer(EventArgs args);

        ITextBuffer Buffer { get; }

        TextPosition GetTextPositionFromPoint(Point point, bool roundDown);
    }

    /// <summary>
    /// Stores the visual representation of an <see cref="ITextBuffer"/> to be displayed in the UI.
    /// </summary>
    class UiTextBuffer : IUiTextBuffer
    {
        public event EventHandler<EventArgs> UpdateBuffer;

        public void RaiseUpdateBuffer(EventArgs args) => HandleEvent(UpdateBuffer, this, args);

        public ITextBuffer Buffer { get; }

        private Dictionary<int, IUiTextLine> lineCache = new Dictionary<int, IUiTextLine>();

        public UiTextBuffer(ITextBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Buffer = buffer;
            Buffer.UpdateBuffer += (s, e) => RaiseUpdateBuffer(e);
        }

        #region IUiTextBuffer

        public void Render(DrawingContext drawingContext, RenderContext renderContext)
        {
            lineCache.Clear();

            var scrollManager = renderContext.ScrollManager;

            var range = scrollManager.GetVisibleTextRange();

            var offset = range.Start.Row;

            var maxWidth = 0;

            var scrollPos = scrollManager.ScrollPositionPixels;

            var startPixel = ScrollLineHeight * range.Start.Row;
            var scrollOffset = new Point(scrollPos.X, scrollPos.Y - startPixel);

            var clipGeometry = new RectangleGeometry(new Rect(scrollManager.ViewportSize));
            drawingContext.PushClip(clipGeometry);
            drawingContext.PushTransform(new ScaleTransform(scrollManager.Zoom, scrollManager.Zoom));

            //This line is very important. Without it, when we scroll to the bottom of a list, part of the last line is cutoff and doesn't display properly
            drawingContext.PushTransform(new TranslateTransform(-scrollOffset.X, -scrollOffset.Y));

            Buffer.PrepareLines(range.Start.Row, range.End.Row);

            for (int i = range.Start.Row, j = 0; i < range.End.Row; i++, j++)
            {
                var line = Buffer.GetLine(j);

                maxWidth = Math.Max(maxWidth, line.GetLength());

                var uiLine = new UiTextLine(line);

                lineCache[i] = uiLine;

                //The IUiTextLine doesn't know what its Y coordinate should be. We apply a transform
                //to the Y axis such that each line will be displayed further and further down the page

                var offsetY = (i - offset) * ScrollLineHeight;

                drawingContext.PushTransform(new TranslateTransform(0, offsetY));
                uiLine.Render(drawingContext, renderContext);
                drawingContext.Pop();
            }

            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();

            ScrollAreaWidth = maxWidth;
        }

        public TextPosition GetTextPositionFromPoint(Point point, bool roundDown)
        {
            int col;
            var row = (int) Math.Floor(point.Y / ScrollLineHeight);

            if (row < 0)
                return default;

            if (Buffer.LineCount > 0)
            {
                if (row > Buffer.LineCount)
                    row = Buffer.LineCount - 1;

                //todo: if you scroll down while moving the mouse down, this crashes
                //also seems to crash scrolling down then back up with the mouse
                var line = lineCache[row];

                var charPositions = line.CharPositions;

                //The first glyph is always at position 0 so no point starting at 0
                for (col = 1; col < charPositions.Length; col++)
                {
                    var charPos = charPositions[col];

                    if (charPos > point.X)
                    {
                        if (roundDown)
                        {
                            col--;
                            break;
                        }
                        else
                        {
                            //We need to see whether we're the closest character to the desired position

                            var currentDiff = charPositions[col] - point.X;
                            var previousDiff = point.X - charPositions[col - 1] + 2;

                            if (currentDiff > previousDiff)
                            {
                                col--;
                                break;
                            }
                        }
                    }
                    
                }

                //After the last character position, we store its end position.
                //There are no further characters after this end position, thus if we're
                //at the end position then we actually want the character that came before us
                if (col == charPositions.Length)
                    col = charPositions.Length - 1;
            }
            else
            {
                row = 0;
                col = 0;
            }

            return new TextPosition(row, col);
        }

        #endregion
        #region IScrollArea

        public double ScrollLineHeight => Buffer.Font.LineHeight;

        public double ScrollAreaWidth { get; private set; }

        public double ScrollAreaHeight => Buffer.LineCount * ScrollLineHeight;

        #endregion
    }
}
