using System.Windows.Controls;

namespace ChaosDbg.Tests
{
    public static class XamlFactory
    {
        public static XamlElement SplitView(Orientation? orientation, params XamlElement[] children) =>
                SplitView(orientation, null, null, children);

        public static XamlElement SplitView(Orientation? orientation = null, string dockedWidth = null, string dockedHeight = null, params XamlElement[] children)
        {
            return new XamlElement("view:SplitView", children)
            {
                { "Orientation", orientation },
                { "DockedWidth", dockedWidth },
                { "DockedHeight", dockedHeight }
            };
        }
    }
}
