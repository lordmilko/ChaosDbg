using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a control that can be positioned within the <see cref="SplitterPanel"/> of
    /// a <see cref="SplitterItemsControl"/>. Types derived from this control simply encapsulate normal
    /// controls (such as <see cref="TabControl"/> or nested <see cref="SplitterItemsControl"/> instances)
    /// </summary>
    [ContentProperty("Children")]
    [DefaultProperty("Children")]
    [DependencyProperty("DockedWidth", typeof(SplitPaneLength), DefaultValueExpression = "new SplitPaneLength(100.0)")]
    [DependencyProperty("DockedHeight", typeof(SplitPaneLength), DefaultValueExpression = "new SplitPaneLength(100.0)")]
    [DependencyProperty("MinimumWidth", typeof(double), DefaultValue = 30.0)]
    [DependencyProperty("MinimumHeight", typeof(double), DefaultValue = 30.0)]
    public abstract partial class DockContainer : DependencyObject
    {
        /* DockedWidth:  The width of the container to use when encapsulated within a Horizontal SplitterItemsDockContainer.
         *               Maps to the PaneLength of the SplitterItem control that encapsulates this object
         *               
         * DockedHeight: The height of the container to use when encapsulated within a Vertical SplitterItemsDockContainer.
         *               Maps to the PaneLength of the SplitterItem control that encapsulates this object
         */

        public ObservableCollection<DependencyObject> Children { get; }

        protected DockContainer()
        {
            Children = new ObservableCollection<DependencyObject>();
        }
    }
}
