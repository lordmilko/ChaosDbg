using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;

namespace ChaosDbg.Tests
{
    class SplitterItemsDockContainerInfoProxy : DockContainerInfoProxy<SplitterItemsDockContainerInfo>
    {
        public SplitterItemsControlInfo ItemsControl => info.ItemsControl;

        public Orientation Orientation => info.Orientation;

        public SplitterItemsDockContainerInfoProxy(SplitterItemsDockContainerInfo info) : base(info)
        {
        }
    }

    [DebuggerTypeProxy(typeof(SplitterItemsDockContainerInfoProxy))]
    [DebuggerDisplay("{ToString(),nq}")]
    class SplitterItemsDockContainerInfo : DockContainerInfo<SplitterItemsDockContainer>
    {
        public SplitterItemsControlInfo ItemsControl => Children.OfType<SplitterItemsControlInfo>().Single();
        
        public Orientation Orientation => Element.Orientation;

        public SplitterItemsDockContainerInfo(SplitterItemsDockContainer element, IPaneItem[] children) : base(element, children)
        {
        }

        public override string ToString()
        {
            return $"DockedWidth = {DockedWidth}, DockedHeight = {DockedHeight}, Orientation = {Orientation}";
        }
    }
}
