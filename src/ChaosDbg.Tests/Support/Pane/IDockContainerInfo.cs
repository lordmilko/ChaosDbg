namespace ChaosDbg.Tests
{
    interface IDockContainerInfo : IPaneItem
    {
        SplitterGripInfo SplitterGrip { get; }

        SplitPaneLength DockedWidth { get; }

        SplitPaneLength DockedHeight { get; }
    }
}
