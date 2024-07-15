using System.Diagnostics;
using System.Linq;
using VsDock;

namespace ChaosDbg.Tests
{
    class SplitterPanelInfoProxy
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SplitterPanelInfo info;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public SplitterItemInfo[] Children => info.Children;

        public IndependentRect Bounds => info.Bounds;

        public SplitterPanelInfoProxy(SplitterPanelInfo info)
        {
            this.info = info;
        }
    }

    [DebuggerDisplay("Count = {Children.Length}")]
    [DebuggerTypeProxy(typeof(SplitterPanelInfoProxy))]
    class SplitterPanelInfo : PaneItem<SplitterPanel>
    {
        public new SplitterItemInfo[] Children => base.Children.Cast<SplitterItemInfo>().ToArray();

        public SplitterPanelInfo(SplitterPanel element, IPaneItem[] children) : base(element, children)
        {
        }
    }
}
