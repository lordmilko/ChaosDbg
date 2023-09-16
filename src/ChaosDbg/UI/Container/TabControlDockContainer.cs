using System.Windows.Controls;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a <see cref="DockContainer"/> that can be used to store and resize
    /// <see cref="TabControlEx"/> instances within the <see cref="SplitterPanel"/> of a parent
    /// <see cref="SplitterItemsControl"/>.
    /// </summary>
    [DependencyProperty("TabStripPlacement", typeof(Dock), DefaultValue = Dock.Bottom)]
    public partial class TabControlDockContainer : DockContainer
    {
        //The visual format of this control is very simple: our children are simply encapsulated into a
        //TabControlEx. It seems the best way to do this is to use a DataTemplate, which we can't
        //do in a TabControlDockContainer.xaml file. As such, this is effectively a lookless control with
        //an absolutely minimal look
    }
}
