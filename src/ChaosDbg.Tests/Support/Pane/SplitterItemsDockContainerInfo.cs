using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using VsDock.View;

namespace ChaosDbg.Tests
{
    class SplitViewInfoDebugView : ViewElementInfoDebugView<SplitViewInfo>
    {
        public SplitterItemsControlInfo ItemsControl => info.ItemsControl;

        public Orientation Orientation => info.Orientation;

        public SplitViewInfoDebugView(SplitViewInfo info) : base(info)
        {
        }
    }

    [DebuggerTypeProxy(typeof(SplitViewInfoDebugView))]
    [DebuggerDisplay("{ToString(),nq}")]
    class SplitViewInfo : ViewElementInfo<SplitView>
    {
        public SplitterItemsControlInfo ItemsControl => Children.OfType<SplitterItemsControlInfo>().Single();
        
        public Orientation Orientation => Element.Orientation;

        public SplitViewInfo(SplitView element, IPaneItem[] children) : base(element, children)
        {
        }

        public override string ToString()
        {
            return $"DockedWidth = {DockedWidth}, DockedHeight = {DockedHeight}, Orientation = {Orientation}";
        }
    }
}
