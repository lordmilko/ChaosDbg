﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChaosDbg.Text;

namespace ChaosDbg.Scroll
{
    /// <summary>
    /// Manages scrolling for a control.
    /// </summary>
    public class ScrollManager
    {
        /// <summary>
        /// Gets the control that this <see cref="ScrollManager"/> manages.
        /// </summary>
        public IScrollInfo Scrollee { get; }

        /// <summary>
        /// Gets the object within the <see cref="Scrollee"/> control that actually contains the content
        /// to be displayed.
        /// </summary>
        public IScrollArea ScrollArea { get; }

        /// <summary>
        /// Gets the total size of the area that <see cref="ScrollArea"/> can be rendered in.<para/>
        /// This is usually set inside a user defined override of <see cref="FrameworkElement.ArrangeOverride(Size)"/>.
        /// </summary>
        public Size ViewportSize { get; set; }

        /// <summary>
        /// Gets the current X and Y position within the scrolled area.
        /// </summary>
        public Point ScrollPosition => scrollPosition;

        //Point is a struct, so when we mutate it we must do so on a field
        private Point scrollPosition;

        /// <summary>
        /// Gets the current <see cref="ScrollPosition"/> in absolute pixels (without decimal places).
        /// </summary>
        public Point ScrollPositionPixels => RoundPointToPixel(ScrollPosition);

        /// <summary>
        /// Gets the current zoom level.
        /// </summary>
        public double Zoom { get; } = 1;

        public int LinesPerPage
        {
            get
            {
                if (ViewportHeight == 0 || ScrollArea.ScrollAreaHeight == 0)
                    return 0;

                return (int) Math.Floor(ViewportHeight / ScrollArea.ScrollLineHeight / Zoom);
            }
        }

        /// <summary>
        /// Gets the effective height of the viewport after accounting for the current zoom level.
        /// </summary>
        public double ViewportZoomHeight => ViewportHeight / Zoom;

        /// <summary>
        /// Gets the effective width of the viewport after accounting for the current zoom level.
        /// </summary>
        public double ViewportZoomWidth => ViewportWidth / Zoom;

        public double MaxVerticalScrollPosition => Math.Max(ScrollArea.ScrollAreaHeight - ViewportZoomHeight + edgeGap, 0);

        public double MaxHorizontalScrollPosition => Math.Max(ScrollArea.ScrollAreaWidth - ViewportZoomWidth + edgeGap, 0);

        private DispatcherTimer invalidateVisualDispatcher;
        private const int edgeGap = 4;

        public ScrollManager(IScrollInfo scrollee, IScrollArea scrollArea)
        {
            if (scrollee == null)
                throw new ArgumentNullException(nameof(scrollee));

            if (scrollArea == null)
                throw new ArgumentNullException(nameof(scrollArea));

            Scrollee = scrollee;
            ScrollArea = scrollArea;

            //If you hold down the line up or down buttons, we'll constantly trying to invalidate
            //the visual at a rapid rate. As such, we limit visual updates to once every 30 ms. If
            //multiple update attempts occur during this timeframe, subsequent updates will be ignored
            //in favour of the currently queued one
            invalidateVisualDispatcher = new DispatcherTimer(
                interval: TimeSpan.FromMilliseconds(30),
                priority: DispatcherPriority.Render,
                callback: (s, e) =>
                {
                    ForceInvalidateScrolledArea();

                    invalidateVisualDispatcher.IsEnabled = false;
                },
                Dispatcher.CurrentDispatcher
            );
        }

        public void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //The IScrollInfo should have been updated directly by the ScrollViewer, so just invalidate the scroll area
            RequestInvalidateScrolledArea();
        }

        public virtual void ScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Scroll(0, (double)-e.Delta / 2.5);
            e.Handled = true;
        }

        /// <summary>
        /// Increments <see cref="ScrollPosition"/> by the specified X and Y amounts.
        /// </summary>
        /// <param name="xAmount">The additional amount to scroll horizontally.</param>
        /// <param name="yAmount">The additional amount to scroll vertically.</param>
        protected void Scroll(double xAmount, double yAmount)
        {
            scrollPosition.X += xAmount;
            scrollPosition.Y += yAmount;
            RequestInvalidateScrolledArea();
        }

        public TextRange GetVisibleTextRange()
        {
            var scrollPos = ScrollPositionPixels;

            var lineCount = (int) (ScrollArea.ScrollAreaHeight / ScrollArea.ScrollLineHeight);

            return new TextRange(
                new TextPosition(
                    (int)Math.Floor(scrollPos.Y / ScrollArea.ScrollLineHeight), //If Y=160 and LineHeight=16, we're at row 10
                    0
                ),
                new TextPosition(
                    Math.Min((int)Math.Ceiling((scrollPos.Y + ViewportHeight / Zoom) / ScrollArea.ScrollLineHeight), lineCount),
                    0
                )
            );
        }

        public void RequestInvalidateScrolledArea()
        {
            //If we tried to scroll out of bounds, bring the position back in bounds within the valid
            //minimum and maximum scroll positions on each axis
            scrollPosition.X = Clamp(scrollPosition.X, 0, MaxHorizontalScrollPosition);
            scrollPosition.Y = Clamp(scrollPosition.Y, 0, MaxVerticalScrollPosition);

            invalidateVisualDispatcher.IsEnabled = true;
        }

        public void ForceInvalidateScrolledArea()
        {
            Scrollee.ScrollOwner?.InvalidateScrollInfo();
            ((UIElement)Scrollee).InvalidateVisual();
        }

        private double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private Point RoundPointToPixel(Point pt)
        {
            var window = App.Current.MainWindow;

            if (window == null)
                return pt;

            var matrix = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice ?? default;

            double m11 = matrix.M11;
            double m22 = matrix.M22;

            pt.X = Math.Floor(pt.X * m11) / m11;
            pt.Y = Math.Floor(pt.Y * m22) / m22;

            return pt;
        }

        #region IScrollInfo

        public double ExtentWidth => ScrollArea.ScrollAreaWidth;
        public double ExtentHeight => ScrollArea.ScrollAreaHeight;
        public double ViewportWidth => ViewportSize.Width;
        public double ViewportHeight => ViewportSize.Height;
        public double HorizontalOffset => scrollPosition.X;
        public double VerticalOffset => scrollPosition.Y;

        //Note while it may appear that holding down the LineUp or LineDown buttons causes things to hang,
        //this is actually just an artifact of having a debugger attached! If you launch the program standalone,
        //there's no issue
        public virtual void LineUp() => Scroll(0, -ScrollArea.ScrollLineHeight);

        public virtual void LineDown() => Scroll(0, ScrollArea.ScrollLineHeight);

        public virtual void LineLeft() => Scroll(-16, 0);

        public virtual void LineRight() => Scroll(16, 0);

        public virtual void PageUp() => Scroll(0, -Math.Max(LinesPerPage - 1, 1) * ScrollArea.ScrollLineHeight);

        public virtual void PageDown() => Scroll(0, Math.Max(LinesPerPage - 1, 1) * ScrollArea.ScrollLineHeight);

        public virtual void PageLeft() => Scroll(-ViewportWidth, 0);

        public virtual void PageRight() => Scroll(ViewportWidth, 0);

        public void MouseWheelUp()
        {
            throw new NotSupportedException("This should never be reached. PreviewMouseWheel should intercept all scroll events.");
        }

        public void MouseWheelDown()
        {
            throw new NotSupportedException("This should never be reached. PreviewMouseWheel should intercept all scroll events.");
        }

        public void MouseWheelLeft()
        {
            throw new NotImplementedException();
        }

        public void MouseWheelRight()
        {
            throw new NotImplementedException();
        }

        public virtual void SetHorizontalOffset(double offset)
        {
            scrollPosition.X = offset;
            RequestInvalidateScrolledArea();
        }

        public virtual void SetVerticalOffset(double offset)
        {
            scrollPosition.Y = offset;
            RequestInvalidateScrolledArea();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
