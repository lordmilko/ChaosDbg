using System.Windows;

namespace ChaosDbg
{
    /// <summary>
    /// Describes information about a pane in a <see cref="SplitterItemsControl"/> that should be surrounded by resizable splitters.
    /// </summary>
    class SplitPaneInfo
    {
        /// <summary>
        /// Gets the <see cref="SplitterItem"/> that is encapsulating the content that should be displayed in the pane.
        /// </summary>
        public SplitterItem Pane { get; }

        /// <summary>
        /// Gets the <see cref="SplitterPanel.PaneLengthProperty"/> that has been propagated to the <see cref="SplitterItem"/> from its child,
        /// or the default <see cref="SplitPaneLength"/> value for a <see cref="SplitterPanel"/> if no <see cref="SplitterPanel.PaneLengthProperty"/>
        /// has been attached.
        /// </summary>
        public SplitPaneLength Length { get; set; }

        /// <summary>
        /// Gets the actual length, in pixels, that should be used for the pane.
        /// </summary>
        public double ActualLength { get; }

        /// <summary>
        /// Gets the effective minimum length of the <see cref="SplitterItem"/> as determined via <see cref="SplitterItemMinimalLengthConverter"/>.
        /// </summary>
        public double MinimumLength { get; }

        public double MaximumLength { get; } //This property is not currently assigned

        /// <summary>
        /// Gets or sets the area with which the pane should be rendered in.
        /// </summary>
        public Rect MeasuredBounds { get; set; }

        /// <summary>
        /// Gets or sets whether the pane has been forcefully set to its <see cref="MinimumLength"/> because the length it was proportionally
        /// assigned was less than its desired minimum.
        /// </summary>
        public bool SetToMinimum { get; set; }

        /// <summary>
        /// Gets or sets whether the pane has forcefully been set to its <see cref="MaximumLength"/> because its length was proportionally assigned
        /// more than its desired maximum.
        /// </summary>
        public bool SetToMaximum { get; set; }

        public SplitPaneInfo(SplitterItem pane)
        {
            Pane = pane;
            Length = SplitterPanel.GetPaneLength(pane);
            ActualLength = SplitterPanel.GetActualPaneLength(pane);
            MinimumLength = SplitterPanel.GetMinimumLength(pane);
            MaximumLength = SplitterPanel.GetMaximumLength(pane);
        }
    }
}
