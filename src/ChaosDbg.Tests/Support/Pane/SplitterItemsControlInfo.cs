using System.Diagnostics;
using System.Linq;
using VsDock;

namespace ChaosDbg.Tests
{
    class SplitterItemsControlInfoProxy
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SplitterItemsControlInfo info;

        public SplitterPanelInfo Panel => info.Panel;

        public IndependentRect Bounds => info.Bounds;

        public SplitterItemsControlInfoProxy(SplitterItemsControlInfo info)
        {
            this.info = info;
        }
    }

    [DebuggerTypeProxy(typeof(SplitterItemsControlInfoProxy))]
    class SplitterItemsControlInfo : PaneItem<SplitterItemsControl>
    {
        public SplitterPanelInfo Panel => (SplitterPanelInfo) Children.Single();

        public SplitterItemsControlInfo(SplitterItemsControl element, IPaneItem[] children) : base(element, children)
        {
        }
    }
}
