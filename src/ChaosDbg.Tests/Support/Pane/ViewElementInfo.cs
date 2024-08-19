using System.Diagnostics;
using System.Linq;
using VsDock;
using VsDock.View;

namespace ChaosDbg.Tests
{
    abstract class ViewElementInfoDebugView<T> where T : IViewElementInfo
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected T info;

        public SplitterGripInfo SplitterGrip => info.SplitterGrip;

        public SplitPaneLength DockedWidth => info.DockedWidth;

        public SplitPaneLength DockedHeight => info.DockedHeight;

        public IndependentRect Bounds => info.Bounds;

        protected ViewElementInfoDebugView(T info)
        {
            this.info = info;
        }
    }

    abstract class ViewElementInfo<T> : PaneItem<T>, IViewElementInfo where T : ViewElement
    {
        public SplitterGripInfo SplitterGrip => Children.OfType<SplitterGripInfo>().SingleOrDefault();

        public SplitPaneLength DockedWidth => Element.DockedWidth;

        public SplitPaneLength DockedHeight => Element.DockedHeight;

        protected ViewElementInfo(T element, IPaneItem[] children) : base(element, children)
        {
        }
    }
}
