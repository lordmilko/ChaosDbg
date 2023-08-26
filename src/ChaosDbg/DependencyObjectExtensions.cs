using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ChaosDbg
{
    public static class DependencyObjectExtensions
    {
        public static T GetAncestor<T>(this DependencyObject child) where T : DependencyObject
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            var ancestor = Ancestors(child).OfType<T>().SingleOrDefault();

            if (ancestor == null)
                throw new InvalidOperationException($"Could not find ancestor of type '{typeof(T).Name}' on control '{child}'.");

            return ancestor;
        }

        public static IEnumerable<DependencyObject> Ancestors(this DependencyObject child)
        {
            var parent = GetParent(child);

            while (parent != null)
            {
                yield return parent;

                parent = GetParent(parent);
            }
        }

        public static DependencyObject GetParent(this DependencyObject child)
        {
            if (child == null)
                return null;

            if (child is ContentElement c)
            {
                var parent = ContentOperations.GetParent(c);

                if (parent != null)
                    return parent;

                if (child is FrameworkContentElement f)
                    return f.Parent;

                return null;
            }

            return VisualTreeHelper.GetParent(child);
        }

        public static T GetLogicalDescendant<T>(this DependencyObject child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            var desc = child.GetLogicalDescendants().OfType<T>().SingleOrDefault();

            if (desc == null)
                throw new InvalidOperationException($"Could not find descendant of type '{typeof(T).Name}' on control '{child}'.");

            return desc;
        }

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
