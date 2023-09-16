using System.Windows.Controls;

namespace ChaosDbg.Tests
{
    public static class XamlFactory
    {
        public static XamlElement SplitterItemsDockContainer(Orientation? orientation, params XamlElement[] children) =>
                SplitterItemsDockContainer(orientation, null, null, children);

        public static XamlElement SplitterItemsDockContainer(Orientation? orientation = null, string dockedWidth = null, string dockedHeight = null, params XamlElement[] children)
        {
            return new XamlElement("local:SplitterItemsDockContainer", children)
            {
                { "Orientation", orientation },
                { "DockedWidth", dockedWidth },
                { "DockedHeight", dockedHeight }
            };
        }
    }
}
