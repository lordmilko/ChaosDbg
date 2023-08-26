using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ChaosDbg.Tests
{
    public static class DependencyObjectExtensions
    {
        public static IEnumerable<DependencyObject> GetLogicalDescendants(this DependencyObject parent)
        {
            if (parent != null)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(parent))
                {
                    if (child is DependencyObject d)
                    {
                        yield return d;

                        foreach (var grandchild in GetLogicalDescendants(d))
                            yield return grandchild;
                    }
                }
            }
        }

        public static IEnumerable<DependencyObject> GetVisualDescendants(this DependencyObject parent)
        {
            if (parent != null)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);

                    if (child != null)
                    {
                        yield return child;

                        foreach (var grandchild in GetVisualDescendants(child))
                            yield return grandchild;
                    }
                }
            }
        }
    }
}
