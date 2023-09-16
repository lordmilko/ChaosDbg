using System.Diagnostics;
using System.Linq;

namespace ChaosDbg.Tests
{
    class DockContainerInfoProxy<T> where T : IDockContainerInfo
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected T info;

        public SplitterGripInfo SplitterGrip => info.SplitterGrip;

        public SplitPaneLength DockedWidth => info.DockedWidth;

        public SplitPaneLength DockedHeight => info.DockedHeight;

        public IndependentRect Bounds => info.Bounds;

        protected DockContainerInfoProxy(T info)
        {
            this.info = info;
        }
    }

    abstract class DockContainerInfo<T> : PaneItem<T>, IDockContainerInfo where T : DockContainer
    {
        public SplitterGripInfo SplitterGrip => Children.OfType<SplitterGripInfo>().SingleOrDefault();

        public SplitPaneLength DockedWidth => Element.DockedWidth;

        public SplitPaneLength DockedHeight => Element.DockedHeight;

        protected DockContainerInfo(T element, IPaneItem[] children) : base(element, children)
        {
        }
    }
}
