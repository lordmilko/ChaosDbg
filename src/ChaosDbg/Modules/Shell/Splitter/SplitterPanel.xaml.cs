using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ChaosDbg
{
    /* We define several properties as attached properties for the purposes of being attached to our child SplitterItem instances.
     * The properties are defined on SplitterPanel rather than SplitterItem because logically the child is <SplitterItem SplitterPanel.IsLast="true" />.
     * To be <SplitterItem IsLast="true" /> doesn't really make sense - "is last of what?". The property is read-only because it should only ever be set
     * by the SplitterPanel internally, never through XAML. */
    [DependencyProperty("Orientation", typeof(Orientation), DefaultValue = Orientation.Vertical, AffectsMeasure = true)]
    [AttachedDependencyProperty("IsFirst", typeof(bool), IsReadOnly = true, DefaultValue = false)]
    [AttachedDependencyProperty("IsLast", typeof(bool), IsReadOnly = true, DefaultValue = false)]
    [AttachedDependencyProperty("PaneLength", typeof(SplitPaneLength), DefaultValueExpression = "new SplitPaneLength(100.0)", AffectsParentMeasure = true, AffectsParentArrange = true)]
    [AttachedDependencyProperty("ActualPaneLength", typeof(double), IsReadOnly = true, DefaultValue = 0.0)]
    [AttachedDependencyProperty("MinimumLength", typeof(double), DefaultValue = 0.0)]
    [AttachedDependencyProperty("MaximumLength", typeof(double), DefaultValue = double.MaxValue)]
    public partial class SplitterPanel : DockPanel
    {
        /* PaneLength: set in SplitterItem.xaml to the DockedHeight/DockedWidth of the element contained in the panel
         * ActualPaneLength: set in SplitterPanel.cs!MeasureOverride to the calculated length of the pane. Consumed in ArrangeOverride/drag events
         * MinimumLength: multibound in SplitterItem.xaml to be calculated from the MinimumWidth of the DockContainer + size of the splitter grip + whether a given SplitterItem IsLast in the SplitterPanel
         */

        public SplitterPanel()
        {
            InitializeComponent();

            //Subscribe for notifications that a splitter within this panel is being dragged. This works because this is a routed event,
            //so will be bubbled up until somebody handles it
            AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnSplitterDragStarted));
        }

        protected override Size MeasureOverride(Size constraint)
        {
            /* Set our IsFirst and IsLast attached dependency properties on each SplitterItem in the panel.
             * Each SplitterItem contains an inner control and a resizable splitter grip. Two panels only
             * require a single splitter between them however, so we need to know when to hide the splitter
             * for a particular SplitterItem */

            var isLast = InternalChildren.Count - 1;

            for (var i = 0; i < InternalChildren.Count; i++)
            {
                SetIsFirst(InternalChildren[i], i == 0);
                SetIsLast(InternalChildren[i], i == isLast);
            }

            //Create a wrapper around each of our SplitterItem children for tracking various bits of information about them
            var infos = InternalChildren.Cast<SplitterItem>().Select(i => new SplitPaneInfo(i)).ToArray();

            var analysis = SumLengthsOfPanes(infos);

            //If this panel wants to show two panels horizontally (side by side) we need to carve up the available width of the panel.
            //Otherwise, we ned to carve up the available height.
            analysis.SplittableDimensionLength = Orientation == Orientation.Horizontal ? constraint.Width : constraint.Height;

            analysis.UpdateTypeDimensions();

            if (analysis.IsMinimumLengthWithinBounds)
            {
                if (analysis.TotalProportionallyAllocatedLength > 0)
                {
                    //Proportionally allocated panes will receive an amount of space within the dimension relative to the amount of space
                    //its peers are also asking for. If the amount of space that a pane would be granted exceeds its maximum however, we need
                    //to shrink the pane back down to fit
                    CheckMaximumLengths(infos, ref analysis);

                    //If any panes have lengths below their required minimums, remove their lengths from the proportionality pool that panes
                    //will be allocated their sizes from - we'll have to assign the fixed minimum size for such panes instead.
                    CheckMinimumLengths(infos, ref analysis);
                }
            }

            MeasureChildPanes(infos, ref analysis, constraint);

            return constraint;
        }

        private static SplitterPanelMeasurement SumLengthsOfPanes(SplitPaneInfo[] infos)
        {
            var analysis = new SplitterPanelMeasurement();

            foreach (var info in infos)
            {
                if (info.Length.IsProportional)
                {
                    analysis.TotalProportionallyAllocatedLength += info.Length.Value;
                    analysis.TotalProportionalMinimumLength += info.MinimumLength;
                }
                else
                {
                    analysis.TotalFillAllocatedLength += info.Length.Value;
                    analysis.TotalFillMinimumLength += info.MinimumLength;
                }
            }

            return analysis;
        }

        private static void CheckMaximumLengths(SplitPaneInfo[] infos, ref SplitterPanelMeasurement analysis)
        {
            foreach (var info in infos)
            {
                if (info.Length.IsProportional)
                {
                    //Based on the amount of space this pane wants to take up relative to its peers within the amount of available space, how big
                    //would it be?
                    var proportionallyAllocatedLength = info.Length.Value / analysis.TotalProportionallyAllocatedLength * analysis.ProportionableDimensionLength;

                    if (proportionallyAllocatedLength > info.MaximumLength)
                    {
                        //The proportion we would assign this pane is bigger than it's allowed to be
                        info.SetToMaximum = true;

                        if (analysis.TotalProportionallyAllocatedLength == info.Length.Value)
                        {
                            //The implication is we should be the only pane that needs to be displayed within the panel.
                            //We got 100% of all of the available space, but we don't want 100%! We need to shrink ourselves,
                            //thereby creating a gap
                            info.Length = new SplitPaneLength(info.MaximumLength);
                            analysis.TotalProportionallyAllocatedLength = info.MaximumLength; //TotalProportionallyAllocatedLength also had the same value as our Length
                        }
                        else
                        {
                            //This comes into play when you drag a pane up to create a new row all by itself, and then drag another pane next to it to become the second
                            //pane on the row. During the preview of the space the pane will occupy, the preview SplitterItem will have a MaximumLength value that is
                            //higher than its allowed length. Since this code path is not hit when there's already two or more panes on a row, we discard the length we initially
                            //set for our pane and replace it with whatever the proportional length is for the singular pane that already resides in this row
                            analysis.TotalProportionallyAllocatedLength -= info.Length.Value;
                            info.Length = new SplitPaneLength(analysis.TotalProportionallyAllocatedLength); //Ostensibly this total just contains the length of the one other pane on this row
                            analysis.TotalProportionallyAllocatedLength *= 2; //Both panes on this row now have the same proportions
                        }

                        //We've modified TotalProportionallyAllocatedLength. Re-calculate the amount of space available for each pane type
                        analysis.UpdateTypeDimensions();
                    }
                }
            }
        }

        private static void CheckMinimumLengths(SplitPaneInfo[] infos, ref SplitterPanelMeasurement analysis)
        {
            if (analysis.FillableDimensionLength < analysis.TotalFillMinimumLength)
            {
                //Panes with lengths of type Fill have requested a total minimum length less than the amount of space remaining for them
                //after processing normal proportionally allocated panes. Steal some of the space that has been granted for proportionally
                //sized panes
                analysis.FillableDimensionLength = analysis.TotalFillMinimumLength;
                analysis.ProportionableDimensionLength = analysis.SplittableDimensionLength - analysis.FillableDimensionLength;
            }

            foreach (var info in infos)
            {
                if (info.MinimumLength != 0)
                {
                    var allocated = analysis.GetAllocatedLength(info);

                    if (allocated < info.MinimumLength)
                    {
                        //The amount of space that was allocated to the pane was less than its required minimum. We will need to allocate
                        //this pane a fixed amount of space (its minimum). As such, remove all details about this pane from our bookkeeping
                        //regarding the total area that is available for the proportional sizing of our sibling panes

                        info.SetToMinimum = true;

                        if (info.Length.IsFill)
                        {
                            //As far as proportional sizing goes, this pane doesn't exist (it will be given its minimum as a fixed size)
                            analysis.FillableDimensionLength -= info.MinimumLength;
                            analysis.TotalFillAllocatedLength -= info.Length.Value;
                        }
                        else
                        {
                            //As far as proportional sizing goes, this pane doesn't exist (it will be given its minimum as a fixed size)
                            analysis.ProportionableDimensionLength -= info.MinimumLength;
                            analysis.TotalProportionallyAllocatedLength -= info.Length.Value;
                        }
                    }
                }
            }
        }

        private void MeasureChildPanes(SplitPaneInfo[] infos, ref SplitterPanelMeasurement analysis, Size availableSize)
        {
            //Tracks the remaining area of the panel that is available for drawing our children
            var remainingArea = new Rect(0, 0, availableSize.Width, availableSize.Height);

            foreach (var info in infos)
            {
                var allocated = info.SetToMinimum ? info.MinimumLength : analysis.GetAllocatedLength(info);

                SetActualPaneLength(info.Pane, allocated);

                if (Orientation == Orientation.Horizontal)
                {
                    var paneSize = new Size(allocated, availableSize.Height);

                    info.MeasuredBounds = new Rect(remainingArea.Left, remainingArea.Top, allocated, remainingArea.Height);
                    remainingArea.X += allocated;

                    info.Pane.Measure(paneSize);
                }
                else
                {
                    var paneSize = new Size(availableSize.Width, allocated);

                    info.MeasuredBounds = new Rect(remainingArea.Left, remainingArea.Top, remainingArea.Width, allocated);
                    remainingArea.Y += allocated;

                    info.Pane.Measure(paneSize);
                }
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            //We essentially implement the same logic as is used in MeasureChildPanes, except we've already calculated and stored the length of the pane

            var remainingArea = new Rect(0, 0, arrangeSize.Width, arrangeSize.Height);

            foreach (SplitterItem child in InternalChildren)
            {
                var allocated = GetActualPaneLength(child);

                if (Orientation == Orientation.Horizontal)
                {
                    remainingArea.Width = allocated;
                    child.Arrange(remainingArea);
                    remainingArea.X += allocated;
                }
                else
                {
                    remainingArea.Height = allocated;
                    child.Arrange(remainingArea);
                    remainingArea.Y += allocated;
                }
            }

            return arrangeSize;
        }

        #region Drag

        private void OnSplitterDragStarted(object sender, DragStartedEventArgs args)
        {
            //We only want to handle drag events pertaining to our SplitterGrip. Any other drag events that come from
            //underneath us should be ignored
            if (args.OriginalSource is SplitterGrip splitter)
            {
                //Subscribe for notifications regarding the current drag operation. Panes and their splitters can potentially come
                //and go, hence we only subscribe to such events for the duration of the current drag session where we know this
                //splitter exists

                splitter.DragDelta += OnSplitterResized;
                splitter.DragCompleted += OnSplitterDragCompleted;

                args.Handled = true;
            }
        }

        private void OnSplitterResized(object sender, DragDeltaEventArgs args)
        {
            if (!TryGetSiblingsToResize((SplitterGrip) sender, out var sibling1, out var sibling2))
                return;

            var amount = Orientation == Orientation.Horizontal ? args.HorizontalChange : args.VerticalChange;

            var info1 = new SplitPaneInfo((SplitterItem) InternalChildren[sibling1]);
            var info2 = new SplitPaneInfo((SplitterItem) InternalChildren[sibling2]);

            var totalLength = info1.Length.Value + info2.Length.Value;
            var totalActualLength = info1.ActualLength + info2.ActualLength;
            var totalMinimumLength = info1.MinimumLength + info2.MinimumLength;
            var adjustment1 = Math.Max(0, Math.Min(totalActualLength, info1.ActualLength + amount));
            var adjustment2 = Math.Max(0, Math.Min(totalActualLength, info2.ActualLength - amount));

            var totalAdjustmentLength = adjustment1 + adjustment2;

            if (totalMinimumLength > totalAdjustmentLength)
                return;

            if (adjustment1 < info1.MinimumLength)
            {
                adjustment2 -= info1.MinimumLength - adjustment1;
                adjustment1 = info1.MinimumLength;
            }

            if (adjustment2 < info2.MinimumLength)
            {
                adjustment1 -= info2.MinimumLength - adjustment2;
                adjustment2 = info2.MinimumLength;
            }

            var bothFill = info1.Length.IsFill && info2.Length.IsFill;
            var bothProportional = info1.Length.IsProportional && info2.Length.IsProportional;

            if (bothFill || bothProportional)
            {
                info1.Length = new SplitPaneLength(adjustment1 / totalAdjustmentLength * totalLength, info1.Length.Type);
                info2.Length = new SplitPaneLength(adjustment2 / totalAdjustmentLength * totalLength, info1.Length.Type);
            }
            else
            {
                if (info1.Length.IsFill)
                    info2.Length = new SplitPaneLength(adjustment2);
                else
                    info1.Length = new SplitPaneLength(adjustment1);
            }

            SetPaneLength(info1.Pane, info1.Length);
            SetPaneLength(info2.Pane, info2.Length);

            InvalidateMeasure();

            args.Handled = true;
        }

        private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs args)
        {
            var splitter = (SplitterGrip) sender;

            splitter.DragDelta -= OnSplitterResized;
            splitter.DragCompleted -= OnSplitterDragCompleted;

            args.Handled = true;
        }

        private bool TryGetSiblingsToResize(SplitterGrip splitter, out int sibling1, out int sibling2)
        {
            for (var i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];

                if (child.IsAncestorOf(splitter))
                {
                    switch (splitter.ResizeBehavior)
                    {
                        case GridResizeBehavior.CurrentAndNext:
                            sibling1 = i;
                            sibling2 = i + 1;
                            break;

                        case GridResizeBehavior.PreviousAndCurrent:
                            sibling1 = i - 1;
                            sibling2 = i;
                            break;

                        case GridResizeBehavior.PreviousAndNext:
                            sibling1 = i - 1;
                            sibling2 = i + 1;
                            break;

                        default:
                            throw new NotImplementedException($"Don't know how to handle {nameof(GridResizeBehavior)} '{splitter.ResizeBehavior}'");
                    }

                    if (sibling1 < 0 || sibling2 < 0)
                        return false;

                    var count = InternalChildren.Count;

                    if (sibling1 > count || sibling2 > count)
                        return false;

                    return true;
                }
            }

            sibling1 = -1;
            sibling2 = -1;

            return false;
        }

        #endregion
    }
}
