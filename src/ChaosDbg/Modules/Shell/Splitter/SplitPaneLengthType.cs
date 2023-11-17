namespace ChaosDbg
{
    /// <summary>
    /// Specifies types of lengths that can be specified to a <see cref="DockContainer"/> for sizing within a <see cref="SplitterItemsControl"/>.
    /// </summary>
    public enum SplitPaneLengthType
    {
        /// <summary>
        /// Indicates that the pane is allocated an amount of space proportional to the amount of space competing panes
        /// within the parent container have requested.<para/>
        /// 
        /// By default, all panes are allocated 100% of the available space. If two panes request 100% of the space, each receives
        /// 50%.<para/>
        /// 
        /// If one pane requests 200% and the other requests 100%, the first pane gets 66.6% of the available space while the other
        /// gets 33.3%.
        /// </summary>
        Proportional,

        /// <summary>
        /// Indicates that the pane automatically consumes all remaining space within the parent pane, after <see cref="Proportional"/>
        /// panes have been allocated. If two panes wish to apply a fill, the remaining space is divided between them.
        /// </summary>
        Fill
    }
}
