using System;
using System.Windows;

namespace ChaosDbg
{
    /// <summary>
    /// Stores information that is used when measuring the dimensions of panes in <see cref="SplitterPanel.MeasureOverride(Size)"/>.
    /// </summary>
    struct SplitterPanelMeasurement
    {
        #region Proportional

        /// <summary>
        /// Gets or sets the total amount of relatively requested space (by each individual pane) along the height or width of this panel (depending on the orientation) that has been allocated
        /// as <see cref="SplitPaneLengthType.Proportional"/>.<para/>
        /// The type of value that is assigned to the length of the <see cref="SplitterItem"/> is conditionally set to either the width or the height that has been
        /// set on the wrapped element in SplitterItem.xaml.
        /// </summary>
        public double TotalProportionallyAllocatedLength { get; set; }

        /// <summary>
        /// Gets or sets the total minimum amount of space across all panes that should be ensured when distributing space between panes with lengths of
        /// type <see cref="SplitPaneLengthType.Proportional"/>.
        /// </summary>
        public double TotalProportionalMinimumLength { get; set; }

        #endregion
        #region Fill

        /// <summary>
        /// Gets or sets the total amount of space along the height or width of this panel (depending on the orientation) that has been allocated
        /// as <see cref="SplitPaneLengthType.Fill"/>.<para/>
        /// Typically each filled pane will be allocated a length of "1". When there is only one filled pane within a given parent, this value is meaningless,
        /// however if a fill were to be set through codebehind to a value greater than 1, it would allow a given pane to be allocated a higher proportion of the fillable
        /// space than all other filled siblings under the same parent.
        /// </summary>
        public double TotalFillAllocatedLength { get; set; }

        /// <summary>
        /// Gets or sets the total minimum amount of space across all panes that should be ensured when distributing space between panes with lengths of
        /// type <see cref="SplitPaneLengthType.Fill"/>.
        /// </summary>
        public double TotalFillMinimumLength { get; set; }

        #endregion

        /// <summary>
        /// Gets the total minimum length that has been allocated across all panes, regardless of whether they are <see cref="SplitPaneLengthType.Proportional"/>
        /// or <see cref="SplitPaneLengthType.Fill"/>.
        /// </summary>
        public double TotalMinimumLength => TotalProportionalMinimumLength + TotalFillMinimumLength;

        #region Dimension

        /// <summary>
        /// Gets or sets the length of the dimension, in pixels, that should be carved up.<para/>
        /// If this panel wants to show two panels horizontally (side by side) we need to carve up the available width of the panel.
        /// Otherwise, we ned to carve up the available height.
        /// </summary>
        public double SplittableDimensionLength { get; set; }

        /// <summary>
        /// Gets or sets the amount of space within the <see cref="SplittableDimensionLength"/> that is available for panes with lengths of type <see cref="SplitPaneLengthType.Fill"/>
        /// after subtracting the amount of space required by panes with lengths of type <see cref="SplitPaneLengthType.Proportional"/>.
        /// </summary>
        public double FillableDimensionLength { get; set; }

        /// <summary>
        /// Gets or sets the amount of space within the <see cref="SplittableDimensionLength"/> that is available for panes with lengths of type <see cref="SplitPaneLengthType.Proportional"/>
        /// after considering the <see cref="TotalFillMinimumLength"/> (which may result in some of our space being stolen)
        /// </summary>
        public double ProportionableDimensionLength { get; set; }

        public void UpdateTypeDimensions()
        {
            //How much space is left for panes of type Fill?
            if (TotalFillAllocatedLength == 0)
                FillableDimensionLength = 0; //There are no fillable panes, no space is needed
            else
                FillableDimensionLength = Math.Max(0, SplittableDimensionLength - TotalProportionallyAllocatedLength); //Whatever space is not needed for proportion is free for Fill

            //How much space is left for panes of type Proportional?
            if (FillableDimensionLength == 0)
                ProportionableDimensionLength = SplittableDimensionLength; //There are no fillable panes, so we get all the space in the dimension!
            else
                ProportionableDimensionLength = TotalProportionallyAllocatedLength;
        }

        public double GetAllocatedLength(SplitPaneInfo info)
        {
            if (info.Length.IsFill)
            {
                if (TotalFillAllocatedLength == 0)
                    return 0;

                //Get the proportion of the available fillable space that this particular pane is entitled to
                return info.Length.Value / TotalFillAllocatedLength * FillableDimensionLength;
            }
            else
            {
                if (TotalProportionallyAllocatedLength == 0)
                    return 0;

                //Get the proportion of the available space for proportional allocations that this particular pane is entitled to
                return info.Length.Value / TotalProportionallyAllocatedLength * ProportionableDimensionLength;
            }
        }

        #endregion

        /// <summary>
        /// Gets whether the <see cref="TotalMinimumLength"/> is within the available <see cref="SplittableDimensionLength"/>.
        /// </summary>
        public bool IsMinimumLengthWithinBounds => TotalMinimumLength <= SplittableDimensionLength;
    }
}
