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
            
            if (steps > 0)
            {
                for (var i = 0; i < steps; i++)
                    LineDown();
            }
            else
            {
                for (var i = 0; i < -steps; i++)
                {
                    LineUp();
                }
            }

            e.Handled = true;
        }

        public override void LineUp()
        {
            var offset = content.StepUp(-1);

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void LineDown()
        {
            var offset = content.StepDown(1);

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

            var offset = content.StepUp(count);

            base.SetVerticalOffset(offset * ScrollArea.ScrollLineHeight);
        }

        public override void PageDown()
        {
            var lines = Math.Max(LinesPerPage - 1, 1);

            var offset = content.StepDown(lines);

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
