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
            foreach (var child in GetVisualChildren(parent))
            {
                yield return child;

                foreach (var grandchild in GetVisualChildren(child))
                    yield return grandchild;
            }
        }

        public static IEnumerable<DependencyObject> GetVisualChildren(this DependencyObject parent)
        {
            if (parent != null)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);

                    if (child != null)
                        yield return child;
                }
            }
        }

        /// <summary>
        /// Gets the bounds of an element within the main window.
        /// </summary>
        /// <param name="element">The element to get the absolute position of.</param>
        /// <returns>The bounds of the specified element.</returns>
        public static IndependentRect GetBounds(this FrameworkElement element)
        {
            //Get the absolute position of the element on the entire screen
            var elementPos = element.PointToScreen(new Point(0, 0));

            //Get the absolute position of the main window on the entire screen
            var mainWindowPos = Application.Current.MainWindow.PointToScreen(new Point(0, 0));

            //Get the relative offset of the element within the main window
            var relativePos = new Point(elementPos.X - mainWindowPos.X, elementPos.Y - mainWindowPos.Y);

            //Our point on the screen is expressed relative to the current DPI. The width/height of the element however
            //are expressed DPI unaware - so if we're at 1.25 scaling, we need to multiply these by 1.25 to get the actual
            //lengths they represent
            var dpi = VisualTreeHelper.GetDpi(element);

            var scaledWidth = element.ActualWidth * dpi.PixelsPerDip;
            var scaledHeight = element.ActualHeight * dpi.PixelsPerDip;

            var rect = new Rect(relativePos.X, relativePos.Y, scaledWidth, scaledHeight);

            if (dpi.PixelsPerDip != 1)
            {
                var independentRect = new Rect(relativePos.X / dpi.PixelsPerDip, relativePos.Y / dpi.PixelsPerDip, element.ActualWidth, element.ActualHeight);

                return new IndependentRect(independentRect, rect, dpi.PixelsPerDip);
            }

            return new IndependentRect(rect, rect, dpi.PixelsPerDip);
        }
    }
}
