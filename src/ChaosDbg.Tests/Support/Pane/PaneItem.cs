using System.Collections.Generic;
using System.Windows;

namespace ChaosDbg.Tests
{
    static class PaneItemExtensions
    {
        public static IEnumerable<IPaneItem> DescendantNodesAndSelf(this IPaneItem root)
        {
            yield return root;

            foreach (var child in root.DescendantNodes())
                yield return child;
        }

        public static IEnumerable<IPaneItem> DescendantNodes(this IPaneItem root)
        {
            foreach (var child in root.Children)
            {
                yield return child;

                foreach (var grandchild in child.DescendantNodes())
                    yield return grandchild;
            }
        }
    }

    class PaneItem<T> : IPaneItem where T : DependencyObject
    {
        public T Element { get; }

        DependencyObject IPaneItem.Element => Element;

        public IPaneItem[] Children { get; }

        public IndependentRect Bounds { get; set; }

        public PaneItem(T element, IPaneItem[] children)
        {
            Element = element;
            Children = children;

            if (element is FrameworkElement f)
                Bounds = f.GetBounds();
        }
    }
}
