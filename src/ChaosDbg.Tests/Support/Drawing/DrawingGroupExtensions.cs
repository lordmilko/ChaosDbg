using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ChaosDbg.Render;

namespace ChaosDbg.Tests
{
    static class DrawingGroupExtensions
    {
        /// <summary>
        /// Enumerates all descendant <see cref="Drawing"/> objects of a <see cref="DrawingGroup"/>,
        /// recursing into additional child <see cref="DrawingGroup"/> instances as required.
        /// </summary>
        /// <param name="group">The <see cref="DrawingGroup"/> to enumerate the descendants of.</param>
        /// <returns>An enumeration of <see cref="Drawing"/> objects under the specified <see cref="DrawingGroup"/>.</returns>
        public static IEnumerable<Drawing> DescendantDrawings(this DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                if (child != null)
                {
                    if (child is DrawingGroup g)
                    {
                        foreach (var grandchild in DescendantDrawings(g))
                            yield return grandchild;
                    }
                    else
                        yield return child;
                }
            }
        }

        public static DrawingGroup GetDrawingGroup<TControl>(this Window window)
        {
            var descendant = window.GetLogicalDescendants().OfType<TControl>().Single();

            var manager = ((IRenderManagerProvider)descendant).RenderManager;

            var group = manager.DrawingGroup;

            return group;
        }
    }
}
