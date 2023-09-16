﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChaosDbg.Tests
{
    class PaneVisualTreeVisitor : VisualTreeVisitor<IPaneItem>
    {
        public PaneVisualTreeVisitor()
        {
            Add<SplitterItemsControl>(VisitSplitterItemsControl);
            Add<SplitterPanel>(VisitSplitterPanel);
            Add<SplitterItem>(VisitSplitterItem);
            Add<SplitterGrip>(VisitSplitterGrip);
        }

        public IPaneItem VisitSplitterItemsControl(SplitterItemsControl control)
        {
            var children = VisitChildren(control);

            return new SplitterItemsControlInfo(control, children);
        }

        public IPaneItem VisitSplitterPanel(SplitterPanel control)
        {
            var children = VisitChildren(control);

            return new SplitterPanelInfo(control, children);
        }

        public IPaneItem VisitSplitterItem(SplitterItem control) =>
            new SplitterItemInfo(control, VisitDockContainerOwner(control));

        public override IPaneItem VisitWindow(TestWindow window) => VisitDockContainerOwner(window);

        private IPaneItem VisitDockContainerOwner(ContentControl control)
        {
            var container = (DockContainer) control.Content;

            if (container == null)
                throw new InvalidOperationException($"Expected control '{control}' to contain a {nameof(DockContainer)} however it did not contain any content.");

            var children = VisitChildren(control);

            /* Our DockContainer controls are transparent wrappers that defer to a "real" control within a DataTemplate, and themselves
             * only inherit from DependencyObject. As such they are not part of the visual or logical tree. When it comes to calculating
             * the bounds of the control, generally speaking the bounds of the DockContainer will be whatever the bounds of the parent are.
             * However when our parent is a Window, the bounds will include the non-client area (titlebar, window borders, etc). In this
             * scenario, we can instead flip things and just try and look at what is hopefully our sole child (e.g. an outer SplitterItemsDockContainer
             * containing several more inner SplitterItemsDockContainer instances) */
            var bounds = control is Window ? children.Single().Bounds : control.GetBounds();

            if (container is SplitterItemsDockContainer s)
            {
                return new SplitterItemsDockContainerInfo(s, children)
                {
                    Bounds = bounds
                };
            }

            throw new NotImplementedException($"Don't know how to handle content of type '{container.GetType().Name}'");
        }

        public IPaneItem VisitSplitterGrip(SplitterGrip control)
        {
            var children = VisitChildren(control);

            return new SplitterGripInfo(control, children);
        }

        protected override IPaneItem CreateResultCollection(IPaneItem[] results) => new PaneItemCollection(results);
    }
}