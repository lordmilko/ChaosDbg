using VsDock;

namespace ChaosDbg.Tests
{
    interface IViewElementInfo : IPaneItem
    {
        SplitterGripInfo SplitterGrip { get; }

        SplitPaneLength DockedWidth { get; }

        SplitPaneLength DockedHeight { get; }
    }
}
