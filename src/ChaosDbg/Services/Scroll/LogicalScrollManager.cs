using System;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ChaosDbg.DbgEng;

namespace ChaosDbg.Scroll
{
    class LogicalScrollManager : ScrollManager
    {
        ILogicalScrollContent content;

        public long VerticalLogicalOffset => (long) (Math.Round(VerticalOffset / ScrollArea.ScrollLineHeight));

        public LogicalScrollManager(IScrollInfo scrollee, IScrollArea scrollArea, ILogicalScrollContent content) : base(scrollee, scrollArea)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            this.content = content;
        }

        public override void ScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var degrees = -e.Delta / 8;
            var steps = (degrees / 15) * 3; //do at least 3 movements

            steps = ClampSteps(steps);

            if (steps == 0)
            {
                e.Handled = true;
                return;
            }

            if (steps > 0)
            {
                var offset = content.StepDown(steps);

                base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
            }
            else
            {
                var offset = content.StepUp(-steps);

                base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
            }

            e.Handled = true;
        }

        private int ClampSteps(int steps)
        {
            if (steps < 0)
                return steps; //Scrolling up, don't care

            //We want to scroll down, however logically, we may have already reached the end of the document. Scrolling further will cause
            //us to move pixels down without actually visually changing anything, requiring extra scrolls back up to get the content moving again.
            //As such, we clamp the number of steps to the maximum number of lines remaining in the scroll area

            var pixelsRemaining = MaxVerticalScrollPosition - ScrollPosition.Y;
            var linesRemaining = Math.Floor(pixelsRemaining / ScrollArea.ScrollLineHeight);

            if (linesRemaining < steps)
                return (int) linesRemaining;

            return steps;
        }

        public override void LineUp()
        {
            var offset = content.StepUp(ClampSteps(-1));

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void LineDown()
        {
            var offset = content.StepDown(ClampSteps(1));

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void LineLeft()
        {
            base.LineLeft();
        }

        public override void LineRight()
        {
            base.LineRight();
        }

        public override void PageUp()
        {
            var count = -Math.Max(LinesPerPage - 1, 1);

            var offset = content.StepUp(ClampSteps(count));

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void PageDown()
        {
            var lines = Math.Max(LinesPerPage - 1, 1);

            var offset = content.StepDown(ClampSteps(lines));

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void PageLeft()
        {
            base.PageLeft();
        }

        public override void PageRight()
        {
            base.PageRight();
        }

        public override void SetHorizontalOffset(double pixelOffset)
        {
            base.SetHorizontalOffset(pixelOffset);
        }

        public override void SetVerticalOffset(double pixelOffset)
        {
            //SetVerticalOffset only seems to occur when we drag the scroller
            var logicalOffset = (long) (pixelOffset / ScrollArea.ScrollLineHeight);

            logicalOffset = content.SeekVertical(logicalOffset);

            pixelOffset = logicalOffset * ScrollArea.ScrollLineHeight;

            base.SetVerticalOffset(pixelOffset);
        }
    }
}
