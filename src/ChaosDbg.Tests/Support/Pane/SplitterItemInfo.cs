using System.Diagnostics;
using System.Linq;
using VsDock;

namespace ChaosDbg.Tests
{
    class SplitterItemInfoProxy
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SplitterItemInfo info;

        public IViewElementInfo Content => info.Content;

        public IndependentRect Bounds => info.Bounds;

        public SplitterItemInfoProxy(SplitterItemInfo info)
        {
            this.info = info;
        }
    }

    [DebuggerDisplay("[SplitterItem] {Content.ToString(),nq}")]
    [DebuggerTypeProxy(typeof(SplitterItemInfoProxy))]
    class SplitterItemInfo : PaneItem<SplitterItem>
    {
        public IViewElementInfo Content => (IViewElementInfo) Children.Single();

        public SplitterItemInfo(SplitterItem element, IPaneItem child) : base(element, new[] { child })
        {
        }
    }
}
